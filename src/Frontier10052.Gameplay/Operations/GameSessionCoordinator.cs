using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Frontier10052.Content;
using Frontier10052.Domain;
using Frontier10052.Gameplay.Persistence;
using Frontier10052.Simulation;

namespace Frontier10052.Gameplay.Operations;

public sealed class GameSessionCoordinator
{
    private readonly IGameSaveStore _saveStore;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _playerLocks = new(StringComparer.Ordinal);

    public GameSessionCoordinator(IGameSaveStore saveStore)
    {
        _saveStore = saveStore;
        Content = VerticalSliceContentPack.Create();
        ContentValidationResult validation = ContentPackValidator.Validate(Content);
        if (!validation.IsValid) throw new InvalidOperationException($"The {Content.Version} content pack is invalid: {string.Join("; ", validation.Errors.Select(error => error.Message))}");
    }

    public VerticalSliceContentPack Content { get; }

    public async ValueTask<CommandResult<GameState>> StartAsync(string playerKey, string callsign, bool overwriteConfirmed, CancellationToken cancellationToken)
    {
        CommandResult<string> keyResult = ValidatePlayerKey(playerKey);
        if (!keyResult.IsSuccess) return Failure(keyResult.Error!);
        if (string.IsNullOrWhiteSpace(callsign) || callsign.Trim().Length is < 2 or > 32) return CommandResult<GameState>.Failure(CommandErrorCodes.InvalidCallsign, "Enter a commander callsign between 2 and 32 characters.");

        string key = keyResult.Value!;
        SemaphoreSlim gate = _playerLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            CommandResult<bool> exists = await _saveStore.ExistsAsync(key, cancellationToken);
            if (!exists.IsSuccess) return Failure(exists.Error!);
            if (exists.Value && !overwriteConfirmed) return CommandResult<GameState>.Failure(CommandErrorCodes.ActiveGameExists, "This browser already owns an active journey. Confirm overwrite to begin again.");

            GameState state = VerticalSliceGameFactory.Create(Content, callsign);
            CommandResult<GameSaveEnvelope> saved = await _saveStore.SaveAsync(key, CreateEnvelope(state), overwriteConfirmed, cancellationToken);
            return saved.IsSuccess ? CommandResult<GameState>.Success(state) : Failure(saved.Error!);
        }
        finally { gate.Release(); }
    }

    public async ValueTask<CommandResult<GameState>> ResumeAsync(string playerKey, CancellationToken cancellationToken)
    {
        CommandResult<string> keyResult = ValidatePlayerKey(playerKey);
        if (!keyResult.IsSuccess) return Failure(keyResult.Error!);
        string key = keyResult.Value!;
        SemaphoreSlim gate = _playerLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try { return await LoadValidatedAsync(key, rewriteMigration: true, cancellationToken); }
        finally { gate.Release(); }
    }

    public async ValueTask<CommandResult<GameState>> MutateAsync(string playerKey, Func<GameState, CommandResult<GameState>> command, CancellationToken cancellationToken)
    {
        CommandResult<string> keyResult = ValidatePlayerKey(playerKey);
        if (!keyResult.IsSuccess) return Failure(keyResult.Error!);
        string key = keyResult.Value!;
        SemaphoreSlim gate = _playerLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            CommandResult<GameState> loaded = await LoadValidatedAsync(key, rewriteMigration: false, cancellationToken);
            if (!loaded.IsSuccess) return loaded;
            CommandResult<GameState> changed = command(loaded.Value!);
            if (!changed.IsSuccess) return changed;
            CommandResult<GameSaveEnvelope> saved = await _saveStore.SaveAsync(key, CreateEnvelope(changed.Value!), true, cancellationToken);
            return saved.IsSuccess ? changed : Failure(saved.Error!);
        }
        finally { gate.Release(); }
    }

    private async ValueTask<CommandResult<GameState>> LoadValidatedAsync(string key, bool rewriteMigration, CancellationToken cancellationToken)
    {
        CommandResult<GameSaveEnvelope> loaded = await _saveStore.LoadAsync(key, cancellationToken);
        if (!loaded.IsSuccess) return Failure(loaded.Error!);
        GameSaveEnvelope envelope = loaded.Value!;
        if (envelope.GameVersion != GameSaveEnvelope.CurrentGameVersion || envelope.Seed != VerticalSliceGameFactory.Seed || envelope.State.Seed != envelope.Seed || envelope.CommandSequence != envelope.State.CommandSequence || envelope.State.SchemaVersion != envelope.StateSchemaVersion)
            return CommandResult<GameState>.Failure(CommandErrorCodes.SaveIncompatible, "The local journey uses an incompatible game or save identity. Start a new commander to recover.");

        bool schema1 = envelope.StateSchemaVersion == 1 && envelope.ContentPackVersion == VerticalSliceContentPack.LegacyPackVersion;
        bool schema2 = envelope.StateSchemaVersion == 2 && envelope.ContentPackVersion == VerticalSliceContentPack.Schema2PackVersion;
        bool schema3 = envelope.StateSchemaVersion == 3 && envelope.ContentPackVersion == VerticalSliceContentPack.Schema3PackVersion;
        bool current = envelope.StateSchemaVersion == GameState.CurrentSchemaVersion && envelope.ContentPackVersion == Content.Version;
        if (!schema1 && !schema2 && !schema3 && !current) return CommandResult<GameState>.Failure(CommandErrorCodes.SaveIncompatible, "The local journey uses an incompatible content or save schema version. Start a new commander to recover.");

        GameState state = schema1
            ? MigrateSchema3To4(MigrateSchema2To3(MigrateSchema1To2(envelope.State)))
            : schema2
                ? MigrateSchema3To4(MigrateSchema2To3(envelope.State))
                : schema3
                    ? MigrateSchema3To4(envelope.State)
                    : envelope.State;
        if (state.Journey is null) return CommandResult<GameState>.Failure(CommandErrorCodes.SaveIncompatible, "The journey phase is missing from the save.");
        if ((schema1 || schema2 || schema3) && rewriteMigration)
        {
            CommandResult<GameSaveEnvelope> rewritten = await _saveStore.SaveAsync(key, CreateEnvelope(state), true, cancellationToken);
            if (!rewritten.IsSuccess) return Failure(rewritten.Error!);
        }
        return CommandResult<GameState>.Success(state);
    }

    private GameState MigrateSchema1To2(GameState state)
    {
        JourneyPhase phase = state.DepartureAuthorized ? JourneyPhase.DepartureAuthorized : JourneyPhase.DockedAtOrigin;
        DestinationMarketState marsMarket = VerticalSliceGameFactory.CreateDestinationMarket(Content, state.Market.Listings);
        return state with
        {
            SchemaVersion = 2,
            Journey = new JourneyState(phase, state.Ship.StationId, null, null, marsMarket, [], state.DepartureAuthorized ? "Departure manifest restored and ready for undock." : "Wayfarer is berthed at Earth Heritage Station."),
            CommercialTrust = 0,
        };
    }

    private GameState MigrateSchema2To3(GameState state)
    {
        JourneyState journey = state.Journey ?? new JourneyState(
            state.DepartureAuthorized ? JourneyPhase.DepartureAuthorized : JourneyPhase.DockedAtOrigin,
            state.Ship.StationId,
            null,
            null,
            VerticalSliceGameFactory.CreateDestinationMarket(Content, state.Market.Listings),
            [],
            "Schema-2 journey restored.");
        bool voyageStarted = journey.Route is not null || state.Ship.StationId != Content.Ship.StationId;
        int elapsed = journey.Route?.ElapsedBaselineHours ?? (state.Ship.StationId == Content.Route.DestinationStationId ? Content.Route.DurationHours : 0);
        int fatigueDelta = voyageStarted ? Math.Clamp((elapsed + 11) / 12, 1, 4) : 0;
        IReadOnlyList<CrewMemberState> crew = state.Crew.Select(member => member with
        {
            Fatigue = Math.Clamp(member.Fatigue + fatigueDelta, 0, 100),
            Memories = member.Memories ?? [],
        }).ToArray();

        ContractState initial = state.Contract with
        {
            IssuerFactionId = string.Empty,
            IsTurnaroundOffer = false,
            OfferedAt = state.Contract.OfferedAt ?? new GameTime(7_200),
            SettledAt = state.Contract.Status is ContractStatus.Completed or ContractStatus.Failed ? state.Time : state.Contract.SettledAt,
            Outcome = string.IsNullOrWhiteSpace(state.Contract.Outcome) ? "Migrated from the schema-2 first voyage." : state.Contract.Outcome,
        };
        bool firstSettled = initial.Status is ContractStatus.Completed or ContractStatus.Failed;
        IReadOnlyList<ContractState> contracts = firstSettled ? [initial, .. CreateTurnaroundOffers(state.Time)] : [initial];

        DepartureManifestState? firstManifest = state.DepartureManifest;
        IReadOnlyList<JourneyHistoryState> history = [];
        if (journey.Route is not null && firstManifest is not null)
        {
            bool arrived = state.Ship.StationId == journey.Route.DestinationStationId || journey.Phase is JourneyPhase.Docked or JourneyPhase.Turnaround or JourneyPhase.Delivered;
            DestinationManifestState? destination = firstSettled
                ? new DestinationManifestState(state.Ship.StationId, initial.Id, state.Time, state.Cargo.ToArray(), initial.Status, state.Money, state.LegalExposure, "Deterministically derived from schema-2 settlement state.")
                : null;
            history =
            [
                new JourneyHistoryState(1, journey.Route.Id, journey.Route.OriginStationId, journey.Route.DestinationStationId, initial.Id, journey.Route.DepartedAt,
                    arrived ? state.Time : null, firstSettled ? state.Time : null, firstManifest, journey.Encounter, destination, "Schema-2 first journey history derived during migration."),
            ];
        }

        IReadOnlyList<StationMarketState> stationMarkets = Content.StationMarkets.Select(definition =>
            definition.StationId == state.Market.StationId ? state.Market : new StationMarketState(definition.StationId, [])).ToArray();
        IReadOnlyList<StationVisitState> visits = state.Ship.StationId == Content.Ship.StationId
            ? [new StationVisitState(Content.Ship.StationId, new GameTime(7_200), null)]
            : [new StationVisitState(Content.Ship.StationId, new GameTime(7_200), journey.Route?.DepartedAt), new StationVisitState(state.Ship.StationId, state.Time, null)];
        TurnaroundState? turnaround = firstSettled
            ? new TurnaroundState(state.Ship.StationId, state.Time, state.Time.AddHours(24), null, null, null, null, false, "Schema-2 Mars settlement migrated into a fresh turnaround window.")
            : null;
        JourneyPhase phase = firstSettled ? JourneyPhase.Turnaround : journey.Phase;

        return state with
        {
            SchemaVersion = 3,
            Crew = crew,
            Contract = initial,
            Contracts = contracts,
            StationMarkets = stationMarkets,
            Lien = new LienState(new Credits(72_000), null, []),
            Maintenance = new MaintenanceState(100 - state.Ship.HullWearPercent, 100 - state.Ship.DriveWearPercent, false, []),
            FactionStandings = [new FactionStandingState(FactionIds.TerranContinuityAuthority, state.CommercialTrust), new FactionStandingState(FactionIds.KuiperSyndicates, 0)],
            LegalExposure = 0,
            Turnaround = turnaround,
            JourneyHistory = history,
            StationVisits = visits,
            ManifestConfidencePercent = 100,
            DepartureAuthorized = firstSettled ? false : state.DepartureAuthorized,
            DepartureManifest = firstSettled ? null : state.DepartureManifest,
            Journey = journey with { Phase = phase, VoyageNumber = 1, ActiveContractId = initial.Id },
        };
    }

    internal GameState MigrateSchema3To4(GameState state)
    {
        IReadOnlyList<ContractState> contracts = state.AllContracts.Select(contract =>
        {
            ContractDefinition? definition = Content.Contracts.SingleOrDefault(item => item.Id == contract.Id);
            return definition is null ? contract : contract with
            {
                Objective = CreateObjective(definition),
                AcceptanceExpiresAt = definition.AcceptanceWindowHours > 0 && contract.OfferedAt is not null
                    ? contract.OfferedAt.Value.AddHours(definition.AcceptanceWindowHours)
                    : contract.AcceptanceExpiresAt,
            };
        }).ToArray();

        ContractState active = contracts.SingleOrDefault(item => item.Id == state.Contract.Id) ?? state.Contract;
        bool settledAtSiriusOrigin = state.Ship.StationId.Value is "ceres-freehold-anchorage" or "pluto-gateway"
            && active.Status is ContractStatus.Completed or ContractStatus.Failed
            && state.Journey?.VoyageNumber == 2;
        TurnaroundState? turnaround = state.Turnaround;
        JourneyState? journey = state.Journey;
        bool departureAuthorized = state.DepartureAuthorized;
        if (settledAtSiriusOrigin)
        {
            ContractState offer = CreateSiriusOffer(state.Time, state.Ship.StationId);
            contracts = [.. contracts.Where(item => item.Id != offer.Id), offer];
            turnaround = new TurnaroundState(
                state.Ship.StationId,
                state.Time,
                offer.AcceptanceExpiresAt ?? state.Time.AddHours(36),
                null,
                null,
                null,
                null,
                false,
                "Schema-3 destination settlement migrated into a Sirius intelligence offer without advancing the clock.",
                StationOperationsMode.SiriusPreparation);
            journey = journey is null ? null : journey with
            {
                Phase = JourneyPhase.Turnaround,
                LastOutcome = "Sirius intelligence offer received at the completed second-voyage destination.",
            };
            departureAuthorized = false;
        }

        IReadOnlyList<StationMarketState> markets =
        [
            .. state.AllStationMarkets,
            .. Content.StationMarkets.Where(definition => state.AllStationMarkets.All(item => item.StationId != definition.StationId))
                .Select(definition => new StationMarketState(definition.StationId, [])),
        ];
        IReadOnlyList<FactionStandingState> standings =
        [
            .. state.AllFactionStandings,
            .. new[] { FactionIds.SiriusCorporateCompact, FactionIds.SiriusLabor }
                .Where(id => state.AllFactionStandings.All(item => item.FactionId != id))
                .Select(id => new FactionStandingState(id, 0)),
        ];

        return state with
        {
            SchemaVersion = GameState.CurrentSchemaVersion,
            Contract = active,
            Contracts = contracts,
            StationMarkets = markets,
            FactionStandings = standings,
            Turnaround = turnaround,
            Journey = journey,
            DepartureAuthorized = departureAuthorized,
            InformationCargo = state.InformationCargo ?? [],
            ContractTransformations = state.ContractTransformations ?? [],
            DebtLedger = state.DebtLedger ?? [],
        };
    }

    internal IReadOnlyList<ContractState> CreateTurnaroundOffers(GameTime offeredAt) => Content.Contracts
        .Where(definition => definition.IsTurnaroundOffer && definition.AllOriginStationIds.Contains(new StationId("mars-industrial-port")))
        .Select(definition => new ContractState(
            definition.Id,
            definition.OriginStationId,
            definition.DestinationStationId,
            definition.CommodityId,
            definition.Quantity,
            offeredAt.AddHours(definition.DeadlineHours),
            definition.Reward,
            definition.FailurePenalty,
            ContractStatus.Offered,
            definition.IssuerFactionId,
            true,
            offeredAt,
            Objective: CreateObjective(definition)))
        .ToArray();

    internal ContractState CreateSiriusOffer(GameTime offeredAt, StationId origin)
    {
        ContractDefinition definition = Content.Contracts.Single(item => item.Id.Value == "scc-sirius-industrial-forecast");
        if (!definition.AllOriginStationIds.Contains(origin)) throw new InvalidOperationException($"Sirius offer cannot originate at {origin}.");
        return new ContractState(
            definition.Id,
            origin,
            definition.DestinationStationId,
            definition.CommodityId,
            definition.Quantity,
            offeredAt.AddHours(definition.DeadlineHours),
            definition.Reward,
            definition.FailurePenalty,
            ContractStatus.Offered,
            definition.IssuerFactionId,
            true,
            offeredAt,
            Objective: CreateObjective(definition),
            AcceptanceExpiresAt: offeredAt.AddHours(definition.AcceptanceWindowHours));
    }

    internal static ContractObjectiveState CreateObjective(ContractDefinition definition) => definition.ObjectiveKind switch
    {
        ContractObjectiveDefinitionKind.Information => new ContractObjectiveState(ContractObjectiveKind.Information, null, new Tonnes(0), definition.InformationId),
        _ => new ContractObjectiveState(ContractObjectiveKind.Cargo, definition.CommodityId, definition.Quantity, null),
    };

    private GameSaveEnvelope CreateEnvelope(GameState state)
    {
        string canonical = GameStateCanonicalizer.Serialize(state);
        string checksum = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
        return new GameSaveEnvelope(GameSaveEnvelope.CurrentGameVersion, Content.Version, state.SchemaVersion, state.Seed, state.CommandSequence, checksum, state);
    }

    private static CommandResult<string> ValidatePlayerKey(string playerKey) => string.IsNullOrWhiteSpace(playerKey) || playerKey.Length > 128 || playerKey.Any(char.IsWhiteSpace)
        ? CommandResult<string>.Failure(CommandErrorCodes.InvalidPlayerKey, "The local browser profile is invalid. Refresh to create a new profile key.")
        : CommandResult<string>.Success(playerKey);

    private static CommandResult<GameState> Failure(CommandError error) => CommandResult<GameState>.Failure(error.Code, error.Message);
}
