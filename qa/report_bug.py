#!/usr/bin/env python3
"""Validate and queue a reproducible AQ Server defect without GitHub credentials."""
import argparse,datetime,hashlib,json,os,pathlib,tempfile
PLATFORMS={'website','windows','ubuntu'}
SEVERITIES={'critical','high','normal','low'}
def validate(data,root):
 if not isinstance(data,dict):raise ValueError('Report must be a JSON object')
 for field in ('title','version','expected','actual'):
  if not isinstance(data.get(field),str) or not data[field].strip():raise ValueError('Missing '+field)
  if len(data[field])>12000:raise ValueError('Field too long: '+field)
 if len(data['title'])>180:raise ValueError('Title must be <= 180 characters')
 if data.get('platform') not in PLATFORMS:raise ValueError('Platform must be website, windows or ubuntu')
 if data.get('severity','normal') not in SEVERITIES:raise ValueError('Unknown severity')
 steps=data.get('steps')
 if not isinstance(steps,list) or not steps or any(not isinstance(v,str) or not v.strip() for v in steps):raise ValueError('Provide reproducible steps')
 evidence=data.get('evidence',[])
 if not isinstance(evidence,list) or any(not isinstance(v,str) for v in evidence):raise ValueError('Evidence must be a list of relative paths')
 allowed=(root/'evidence').resolve()
 for name in evidence:
  p=pathlib.Path(name)
  if p.is_absolute() or '..' in p.parts:raise ValueError('Evidence must stay inside evidence/')
  path=(allowed/p).resolve()
  if allowed not in path.parents or not path.is_file():raise ValueError('Evidence file is missing or unsafe')
 payload={k:data[k] for k in ('title','version','platform','expected','actual','steps')}
 payload['severity']=data.get('severity','normal');payload['environment']=str(data.get('environment',''))[:4000];payload['evidence']=evidence
 return payload
def submit(data,root):
 root=pathlib.Path(root).resolve();payload=validate(data,root)
 identity=json.dumps({k:payload[k] for k in ('title','version','platform','steps')},ensure_ascii=False,sort_keys=True)
 key=hashlib.sha256(identity.encode()).hexdigest()[:16]
 payload['id']='AQ-'+key;payload['createdUtc']=datetime.datetime.now(datetime.timezone.utc).isoformat();payload['state']='new'
 inbox=root/'inbox';inbox.mkdir(parents=True,exist_ok=True);dest=inbox/(payload['id']+'.json')
 if dest.exists():return {'id':payload['id'],'status':'duplicate','path':str(dest)}
 fd,temp=tempfile.mkstemp(prefix='.report-',dir=inbox)
 try:
  with os.fdopen(fd,'w',encoding='utf-8') as stream:json.dump(payload,stream,ensure_ascii=False,indent=2);stream.write('\n')
  # Link is atomic and cannot overwrite an existing concurrent report.
  try:os.link(temp,dest)
  except FileExistsError:return {'id':payload['id'],'status':'duplicate','path':str(dest)}
 finally:pathlib.Path(temp).unlink(missing_ok=True)
 return {'id':payload['id'],'status':'queued','path':str(dest)}
def main():
 parser=argparse.ArgumentParser();parser.add_argument('report',type=pathlib.Path);parser.add_argument('--root',type=pathlib.Path,default=pathlib.Path(__file__).resolve().parent);args=parser.parse_args()
 try:result=submit(json.loads(args.report.read_text(encoding='utf-8-sig')),args.root)
 except (ValueError,OSError) as error:parser.exit(2,str(error)+'\n')
 print(json.dumps(result,ensure_ascii=False))
if __name__=='__main__':main()
