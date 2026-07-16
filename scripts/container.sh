#!/bin/sh
set -eu

requested="${1:-test}"
case "$requested" in
    podman|docker)
        action="test"
        engine="$requested"
        ;;
    *)
        action="$requested"
        engine="${2:-${CONTAINER_ENGINE:-podman}}"
        ;;
esac

case "$engine" in
    podman|docker) ;;
    *)
        echo "Unsupported container engine: $engine (expected podman or docker)." >&2
        exit 64
        ;;
esac

if ! command -v "$engine" >/dev/null 2>&1; then
    echo "Container engine '$engine' is not installed or is not on PATH." >&2
    exit 127
fi

case "$action" in
    test)
        image="${CONTAINER_TEST_IMAGE:-frontier10052-tests:local}"
        "$engine" build --file Containerfile --target test --tag "$image" .
        "$engine" run --rm "$image"
        ;;
    run)
        image="${CONTAINER_WEB_IMAGE:-frontier10052-web:local}"
        port="${FRONTIER10052_PORT:-8080}"
        "$engine" build --file Containerfile --target runtime --tag "$image" .
        "$engine" run --rm --publish "$port:8080" "$image"
        ;;
    *)
        echo "Usage: $0 [test|run] [podman|docker] or $0 [podman|docker]" >&2
        exit 64
        ;;
esac
