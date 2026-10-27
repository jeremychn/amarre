#!/bin/sh
# Télécharge Content.zip depuis une release GitHub et le décompresse dans le volume "content".
#
# Usage (root, sur le serveur Docker) :
#   ./maj-agents.sh              -> dernière release qui contient Content.zip
#   ./maj-agents.sh v1.7         -> release précise (ou agents-2026.09.29.1600)
#   GARDER_WIN64=1 ./maj-agents.sh   -> ne touche pas aux agents Windows x64
#                                       (évite la mise à jour de tous les postes Windows)
#
# Dépôt public : aucun jeton nécessaire. GITHUB_TOKEN, s'il est défini, est utilisé
# (limite d'appels de l'API plus élevée).
set -eu

REPO=${REPO:-jeremychn/amarre}
DEST=${DEST:-/srv/remotely/content}
AUTH=
if [ -n "${GITHUB_TOKEN:-}" ]; then AUTH="Authorization: Bearer $GITHUB_TOKEN"; fi
API=https://api.github.com/repos/$REPO/releases

if [ $# -gt 0 ]; then API=$API/tags/$1; fi

JSON=$(curl -fsSL ${AUTH:+-H "$AUTH"} "$API") || {
    echo "Release introuvable : $API (vérifier le tag)." >&2
    exit 1
}
ASSET=$(printf '%s' "$JSON" | python3 -c '
import json, sys
d = json.load(sys.stdin)
for r in (d if isinstance(d, list) else [d]):
    for a in r["assets"]:
        if a["name"] == "Content.zip":
            print(a["url"], r["tag_name"]); sys.exit()
sys.exit("Aucune release avec Content.zip")')
set -- $ASSET
echo "Release $2"

TMP=$(mktemp)
trap 'rm -f "$TMP"' EXIT
curl -fsSL ${AUTH:+-H "$AUTH"} -H "Accept: application/octet-stream" -o "$TMP" "$1"

BAK=$DEST.bak-$(date +%Y%m%d-%H%M)
cp -a "$DEST" "$BAK"
echo "Sauvegarde : $BAK"

if [ "${GARDER_WIN64:-}" = 1 ]; then
    unzip -o -q "$TMP" -x 'Remotely-Win-x64.zip' 'Win-x64/*' -d "$DEST"
else
    unzip -o -q "$TMP" -d "$DEST"
fi
echo "Agents $2 installés dans $DEST"
