#!/bin/bash
# Test de bout en bout : création d'un compte puis connexion, sur un serveur Remotely qui tourne.
# Usage : Utilities/test-login.sh http://localhost:5000
set -u
URL=${1:-http://localhost:5000}
EMAIL=ci@test.local
PASS='Test-Login-123'
J=$(mktemp -d)

token() { grep -o 'name="__RequestVerificationToken" [^>]*value="[^"]*"' "$1" | sed 's/.*value="//;s/"$//' | head -1; }
fail() { echo "::error::$1"; sed 's/<[^>]*>/ /g' "$2" | tr -s ' \n' ' ' | grep -o 'Erreur[^.]*\.' | head -3 | sed 's/^/::error::/'; exit 1; }

curl -s -c $J/c1 -b $J/c1 "$URL/Account/Register" -o $J/reg.html
curl -s -c $J/c1 -b $J/c1 -o $J/reg2.html -w '%{http_code} %{redirect_url}\n' "$URL/Account/Register" \
  --data-urlencode "__RequestVerificationToken=$(token $J/reg.html)" --data-urlencode "_handler=register" \
  --data-urlencode "Input.Email=$EMAIL" --data-urlencode "Input.Password=$PASS" --data-urlencode "Input.ConfirmPassword=$PASS" > $J/reg.out
read -r code _ < $J/reg.out
[ "$code" = 302 ] || fail "Inscription : HTTP $code au lieu d'une redirection" $J/reg2.html

curl -s -c $J/c2 -b $J/c2 "$URL/Account/Login" -o $J/login.html
curl -s -c $J/c2 -b $J/c2 -o $J/login2.html -w '%{http_code} %{redirect_url}\n' "$URL/Account/Login" \
  --data-urlencode "__RequestVerificationToken=$(token $J/login.html)" --data-urlencode "_handler=login" \
  --data-urlencode "Input.Email=$EMAIL" --data-urlencode "Input.Password=$PASS" > $J/login.out
read -r code _ < $J/login.out
[ "$code" = 302 ] && grep -q 'Identity.Application' $J/c2 || fail "Connexion : HTTP $code, pas de cookie de session" $J/login2.html

code=$(curl -s -b $J/c2 -o /dev/null -w '%{http_code}' "$URL/")
[ "$code" = 200 ] || fail "Page d'accueil connectée : HTTP $code" /dev/null
echo "Inscription et connexion OK"
