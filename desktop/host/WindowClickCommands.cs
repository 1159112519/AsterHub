// Pure policy: unit tests need no HWND, native input, application instance or user profile.
internal enum WindowClickAction { Ignore, Minimize, Restore }
internal static class WindowClickCommands {
 internal static WindowClickAction ForTaskbarClick(bool exiting,bool visible,bool minimized){
  if(exiting)return WindowClickAction.Ignore;
  return visible&&!minimized?WindowClickAction.Minimize:WindowClickAction.Restore;
 }
 internal static bool IsMinimizeSystemCommand(int message,long command){return message==0x0112&&(command&0xFFF0)==0xF020;}
}
