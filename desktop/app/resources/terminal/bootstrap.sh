# Session-only prompt settings: never writes remote shell configuration.
export CONDA_CHANGEPS1=false
export VIRTUAL_ENV_DISABLE_PROMPT=1
[ -f ~/.bashrc ] && . ~/.bashrc
export TERM=xterm-256color
__aq_env() {
 local aq_env=''
 if [ -n "$CONDA_PREFIX" ]; then aq_env="${CONDA_DEFAULT_ENV:-${CONDA_PREFIX##*/}}"; fi
 if [ -n "$VIRTUAL_ENV" ]; then aq_env="${aq_env:+$aq_env / }${VIRTUAL_ENV##*/}"; fi
 printf '%s' "$aq_env"
}
__aq_prompt() { local aq_env; aq_env=$(__aq_env); [ -z "$aq_env" ] || printf '(%s) ' "$aq_env"; }
__aq_status() {
 local aq_data aq_env
 aq_env=$(__aq_env)
 aq_data=$(printf '%s\n%s\n%s\n%s\n%s' "$(id -un)" "$(hostname)" "$PWD" "bash" "$aq_env" | base64 | tr -d '\r\n')
 printf '\033]1337;AQSTATUS=%s\007' "$aq_data"
}
if [[ $(declare -p PROMPT_COMMAND 2>/dev/null) == 'declare -a'* ]]; then PROMPT_COMMAND+=(__aq_status); else PROMPT_COMMAND="${PROMPT_COMMAND:+$PROMPT_COMMAND; }__aq_status"; fi
PS1='$(__aq_prompt)\u@\h:\w\$ '
