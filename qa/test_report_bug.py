import json,pathlib,tempfile,unittest
from report_bug import submit
class QueueTests(unittest.TestCase):
 def report(self):return dict(title='A reproducible layout defect',version='0.1.0',platform='website',severity='normal',steps=['Open at 390px width','Open the navigation'],expected='Navigation remains readable',actual='One entry overlaps',evidence=[])
 def test_same_defect_deduplicates_without_overwriting_original(self):
  with tempfile.TemporaryDirectory() as directory:
   first=submit(self.report(),directory);path=pathlib.Path(first['path']);original=path.read_bytes();second=submit(self.report(),directory)
   self.assertEqual(second['status'],'duplicate');self.assertEqual(path.read_bytes(),original)
 def test_evidence_cannot_escape_even_via_symlink(self):
  with tempfile.TemporaryDirectory() as directory:
   root=pathlib.Path(directory);(root/'evidence').mkdir();data=self.report();data['evidence']=['../private.txt']
   with self.assertRaises(ValueError):submit(data,root)
   outside=root/'secret.txt';outside.write_text('not evidence');link=root/'evidence/link.txt'
   try:link.symlink_to(outside)
   except OSError:return
   data['evidence']=['link.txt']
   with self.assertRaises(ValueError):submit(data,root)
 def test_requires_actual_steps_version_and_observed_result(self):
  for key in ['steps','version','actual']:
   data=self.report();data[key]=[] if key=='steps' else ''
   with tempfile.TemporaryDirectory() as directory:
    with self.assertRaises(ValueError):submit(data,directory)
if __name__=='__main__':unittest.main()
