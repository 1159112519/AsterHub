import importlib.util,unittest,base64,contextlib,io,re,types
from pathlib import Path
from unittest.mock import patch
spec=importlib.util.spec_from_file_location('agent',Path(__file__).with_name('asterhub_agent.py'));a=importlib.util.module_from_spec(spec);spec.loader.exec_module(a)
class GpuPathTests(unittest.TestCase):
 def test_resolves_executable_without_reading_commandline(self):
  path='/home/alice/模型 one/venv/bin/python'
  with patch.object(a.os,'readlink',return_value=path) as readlink:
   self.assertEqual(a.process_executable('701','python'),path)
   readlink.assert_called_once_with('/proc/701/exe')
 def test_denied_or_exited_process_retains_nvidia_name(self):
  for error in [PermissionError(),FileNotFoundError()]:
   with patch.object(a.os,'readlink',side_effect=error):
    self.assertEqual(a.process_executable('702','/opt/task two/python'),'/opt/task two/python')
 def test_sampler_keeps_paths_for_duplicate_python_names(self):
  paths={'/proc/701/exe':'/home/alice/模型 one/venv/bin/python','/proc/702/exe':'/srv/task two/venv/bin/python'}
  def run(args):
   if any(v.startswith('--query-gpu=') for v in args):return [['0','GPU-test','fixture','10','100','200','30','0','10','100','200','300']]
   return [['GPU-test','701','python','100'],['GPU-test','702','python','100']]
  files={'/proc/stat':'cpu 1 2 3 4 0 0 0 0','/proc/meminfo':'MemTotal: 1024 kB\nMemAvailable: 512 kB'}
  with patch.object(a,'run',side_effect=run),patch.object(a,'text',side_effect=lambda p:files.get(p,'')),patch.object(a.os,'readlink',side_effect=lambda p:paths[p]):
   rows=a.Sampler().sample()['Processes']
  self.assertEqual([p['Name'] for p in rows],list(paths.values()))
  self.assertEqual([p['Pid'] for p in rows],['701','702'])
  self.assertTrue(all('CommandLine' not in p for p in rows))
 def test_invalid_pid_never_reads_proc(self):
  with patch.object(a.os,'readlink') as readlink:
   self.assertEqual(a.process_executable('../secret','python'),'python')
   readlink.assert_not_called()
class RemoteGpuPathTests(unittest.TestCase):
 def program(self):
  source=(Path(__file__).parents[2]/'desktop-preview/backend/src/AQServer.Platform.Windows/Remote.cs').read_text(encoding='utf-8-sig')
  return re.search(r'CommandMetadataProgram=@"(.*?)";',source,re.S).group(1).replace('""','"')
 def collect(self,path,denied=False):
  stat='701 (python worker) '+' '.join(['S']+['0']*18+['123456'])
  def opened(name,mode='r'):
   if name.endswith('/boot_id'):return io.StringIO('boot-fixture')
   if name.endswith('/stat'):return io.StringIO(stat)
   if name.endswith('/cmdline'):return io.BytesIO(b'python\x00worker.py\x00')
   raise AssertionError('unexpected file '+name)
  output=io.StringIO()
  with patch('builtins.open',side_effect=opened),patch.object(a.os,'stat',return_value=types.SimpleNamespace(st_uid=1000)),patch.object(a.os,'readlink',side_effect=PermissionError() if denied else None,return_value=path),patch('sys.stdin',io.StringIO('GPU-test,701,python,1024\n')),contextlib.redirect_stdout(output):
   exec(self.program(),{})
  return output.getvalue().strip().split('\t')
 def test_ssh_metadata_contains_executable_path_and_preserves_identity(self):
  path='/srv/模型 one/venv/bin/python';row=self.collect(path)
  self.assertEqual(row[:2],['701','boot-fixture:123456:1000'])
  self.assertEqual(len(row),4)
  self.assertEqual(base64.b64decode(row[3]).decode('utf-8'),path)
 def test_ssh_path_permission_failure_still_emits_identity(self):
  row=self.collect('',True)
  self.assertEqual(row[:2],['701','boot-fixture:123456:1000'])
  self.assertEqual(len(row),4)
  self.assertEqual(row[3],'-')
if __name__=='__main__':unittest.main()
