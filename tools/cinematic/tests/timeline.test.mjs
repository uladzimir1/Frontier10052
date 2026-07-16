import test from "node:test";
import assert from "node:assert/strict";
import { normalizedProgress, qualityProfile, seenCueKey, stagePose } from "../src/timeline.js";

test("scroll progress clamps to one normalized timeline", () => {
    assert.equal(normalizedProgress(100, 3000, 1000), 0);
    assert.equal(normalizedProgress(-1000, 3000, 1000), 0.5);
    assert.equal(normalizedProgress(-3000, 3000, 1000), 1);
});

test("automatic quality promotes only capable desktop hardware", () => {
    assert.equal(qualityProfile("Automatic", { width: 1536, memory: 16, cores: 12, touch: false }).name, "High");
    assert.equal(qualityProfile("Automatic", { width: 390, memory: 8, cores: 8, touch: true }).name, "Balanced");
    assert.equal(qualityProfile("High", {}).dpr, 2);
    assert.equal(qualityProfile("Balanced", {}).dpr, 1.5);
    assert.equal(qualityProfile("Low bandwidth", {}).dpr, 1);
    assert.equal(qualityProfile("Low bandwidth", {}).fallback, true);
});

test("seen keys include game, command, and cue identity", () => {
    const cue = { commandSequence: 17, cueId: "route:checkpoint" };
    assert.equal(seenCueKey(cue, "game-1"), "frontier10052:cinematic:seen:game-1:17:route:checkpoint");
});

test("reduced motion uses authored static cuts", () => {
    const start = stagePose("docking-handoff", 0.49, true);
    const end = stagePose("docking-handoff", 0.51, true);
    assert.notDeepEqual(start.camera, end.camera);
    assert.deepEqual(stagePose("docking-handoff", 0.1, true), stagePose("docking-handoff", 0.4, true));
});
