import importlib.util,json,sqlite3,tempfile,unittest,os,time
from pathlib import Path
spec=importlib.util.spec_from_file_location('agent',Path(__file__).with_name('asterhub_agent.py'));a=importlib.util.module_from_spec(spec);spec.loader.exec_module(a)
class RecorderTests(unittest.TestCase):
 def row(self,t,cpu=10):return dict(T=t,Cpu=str(cpu),Interval=5,Mem='100 / 1000 MiB',Gpus=[],Processes=[])
 def test_roundtrip_restart_readonly_and_dedup(self):
  with tempfile.TemporaryDirectory() as tmp:
   store=a.Store(tmp);t=a.TICKS+10000000;store.append(self.row(t));store.append(self.row(t));store.db.close();read=a.Store(tmp,True);self.assertEqual(read.read(t)['Cpu'],'10');self.assertEqual(read.status()['count'],1);read.db.close()
 def test_summary_retains_peaks_and_real_gap(self):
  with tempfile.TemporaryDirectory() as tmp:
   store=a.Store(tmp)
   for i in range(1500):store.append(self.row(a.TICKS+(i*5+(200 if i>750 else 0))*10000000,99 if i==300 else 2))
   result=store.summary(a.TICKS,a.TICKS+1000000000000,160);self.assertLessEqual(len(result['points']),160);self.assertIn(99,[p['cpu'] for p in result['points']]);self.assertTrue(any(p['gapBefore'] for p in result['points']));store.db.close()
 def test_retention_never_touches_unrelated_file(self):
  with tempfile.TemporaryDirectory() as tmp:
   sentinel=Path(tmp)/'user-data.txt';sentinel.write_text('preserve');store=a.Store(tmp);store.append(self.row(a.TICKS));store.retain();self.assertEqual(store.status()['count'],0);self.assertEqual(sentinel.read_text(),'preserve');store.db.close()
 def fill(self,store,count=200):
  now=a.TICKS+time.time_ns()//100
  for i in range(count):
   row=self.row(now+i*10000000);row['Padding']=os.urandom(6000).hex();store.append(row)
 def test_capacity_retains_recent_half_and_reclaims_pages(self):
  with tempfile.TemporaryDirectory() as tmp:
   store=a.Store(tmp);self.fill(store);store.db.execute('pragma wal_checkpoint(TRUNCATE)').fetchall()
   limit=store.path.stat().st_size//2;store.retain(limit=limit)
   self.assertGreater(store.status()['count'],65);self.assertLess(store.status()['count'],150)
   self.assertLessEqual(store.path.stat().st_size,limit+16384);store.db.close()
 def test_pinned_reader_does_not_wipe_history(self):
  with tempfile.TemporaryDirectory() as tmp:
   store=a.Store(tmp);self.fill(store);store.db.execute('pragma wal_checkpoint(TRUNCATE)').fetchall()
   reader=sqlite3.connect(store.path);reader.execute('begin');reader.execute('select count(*) from samples').fetchone()
   limit=store.path.stat().st_size//2;store.retain(limit=limit)
   kept=store.status()['count'];self.assertGreater(kept,65)
   store.retain(limit=limit);self.assertEqual(store.status()['count'],kept)
   reader.close();store.retain(limit=limit);self.assertEqual(store.status()['count'],kept)
   self.assertLessEqual(store.path.stat().st_size,limit+16384);store.db.close()
 def test_missing_data_is_explicit(self):
  with tempfile.TemporaryDirectory() as tmp:
   with self.assertRaises(sqlite3.OperationalError):a.Store(tmp,True)
if __name__=='__main__':unittest.main()
