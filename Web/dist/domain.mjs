export const VERSION=1;
export const ITEMS=[
 {id:'hat-beret',name:'画家贝雷帽',slot:'hat',price:30,icon:'🧢',description:'把灵感戴在头顶'},
 {id:'hat-crown',name:'小小皇冠',slot:'hat',price:80,icon:'👑',description:'为每天的努力加冕'},
 {id:'outfit-sweater',name:'奶油针织衫',slot:'outfit',price:40,icon:'👕',description:'软软暖暖，陪你学习'},
 {id:'accessory-bow',name:'樱桃蝴蝶结',slot:'accessory',price:20,icon:'🎀',description:'今天也要可可爱爱'},
 {id:'accessory-glasses',name:'学霸圆眼镜',slot:'accessory',price:35,icon:'👓',description:'开启认真学习模式'},
 {id:'accessory-satchel',name:'迷你学习包',slot:'accessory',price:50,icon:'🎒',description:'装好今天的小进步'}
];
export const today=(date=new Date())=>`${date.getFullYear()}-${String(date.getMonth()+1).padStart(2,'0')}-${String(date.getDate()).padStart(2,'0')}`;
const int=(v,min,max,fallback)=>Number.isFinite(v)?Math.max(min,Math.min(max,Math.trunc(v))):fallback;
export const uid=()=>globalThis.crypto?.randomUUID?.()||`${Date.now().toString(36)}-${Math.random().toString(36).slice(2)}`;
const text=(s,max=100)=>typeof s==='string'?s.trim().slice(0,max):'';
export function fresh(){return {webVersion:VERSION,days:{},alarms:[],pet:{kind:'cat',catName:'银米',bunnyName:'糯米',dogName:'布丁',hat:'none',outfit:'none',accessory:'none'},settings:{focusMinutes:25,shortBreakMinutes:5,longBreakMinutes:15,cycles:4,eyeMinutes:20,goalMinutes:120,eyeEnabled:true,soundEnabled:true},coins:0,owned:[],records:[],timer:{phase:'focus',running:false,remaining:1500,duration:1500,minutes:25,cycle:0,eyeElapsed:0,id:'',taskTitle:''}};}
const validDate=s=>typeof s==='string'&&s.length<=40&&Number.isFinite(Date.parse(s))?new Date(s).toISOString():'';
export function normalize(raw){
 if(!raw||typeof raw!=='object'||Array.isArray(raw)||raw.webVersion!==VERSION)throw Error(raw?.webVersion>VERSION?'这份存档来自更新的网页版，请先更新应用。':'请选择糯米学习网页版导出的存档。');
 const s=fresh();
 for(const key of ['focusMinutes','shortBreakMinutes','longBreakMinutes','cycles','eyeMinutes','goalMinutes']){
  const range={focusMinutes:[5,90],shortBreakMinutes:[1,30],longBreakMinutes:[1,30],cycles:[2,8],eyeMinutes:[10,60],goalMinutes:[30,480]}[key];
  s.settings[key]=int(raw.settings?.[key],...range,s.settings[key]);
 }
 for(const k of ['eyeEnabled','soundEnabled'])s.settings[k]=raw.settings?.[k]!==false;
 s.pet.kind=['cat','bunny','dog'].includes(raw.pet?.kind)?raw.pet.kind:'cat';
 for(const k of ['catName','bunnyName','dogName'])s.pet[k]=text(raw.pet?.[k],12)||s.pet[k];
 s.coins=int(raw.coins,0,2147483647,0);
 s.owned=[...new Set((Array.isArray(raw.owned)?raw.owned:[]).filter(x=>ITEMS.some(y=>x===y.id)))];
 for(const slot of ['hat','outfit','accessory'])s.pet[slot]=ITEMS.some(x=>x.slot===slot&&x.id===raw.pet?.[slot]&&s.owned.includes(x.id))?raw.pet[slot]:'none';
 if(raw.days&&typeof raw.days==='object')for(const [day,tasks] of Object.entries(raw.days).slice(-3660)){
  if(!/^\d{4}-\d{2}-\d{2}$/.test(day)||!Array.isArray(tasks))continue;
  const ids=new Set();s.days[day]=tasks.slice(0,1000).filter(x=>x&&text(x.title)&&typeof x.id==='string'&&!ids.has(x.id)&&(ids.add(x.id),true)).map(x=>({id:text(x.id),title:text(x.title,100),important:!!x.important,urgent:!!x.urgent,done:!!x.done,dueAt:validDate(x.dueAt),lead:int(x.lead,0,1440,15),reminded:!!x.reminded}));
 }
 s.alarms=(Array.isArray(raw.alarms)?raw.alarms:[]).slice(0,200).filter(x=>x&&text(x.title)&&validDate(x.at)).map(x=>({id:text(x.id)||uid(),title:text(x.title),at:validDate(x.at),enabled:x.enabled!==false,notified:!!x.notified}));
 const seen=new Set();s.records=(Array.isArray(raw.records)?raw.records:[]).slice(-10000).filter(x=>x&&typeof x.id==='string'&&/^\d{4}-\d{2}-\d{2}$/.test(x.date)&&!seen.has(x.id)&&(seen.add(x.id),true)).map(x=>({id:text(x.id),date:x.date,minutes:int(x.minutes,5,90,25),coins:int(x.coins,0,90,0),taskTitle:text(x.taskTitle)}));
 const r=raw.timer||{},t=s.timer;t.phase=['focus','focusDone','break','breakDone'].includes(r.phase)?r.phase:'focus';
 t.minutes=int(r.minutes,5,90,s.settings.focusMinutes);t.duration=t.phase.startsWith('break')?int(r.duration,60,1800,300):t.minutes*60;
 t.remaining=Number.isFinite(r.remaining)?Math.max(0,Math.min(t.duration,r.remaining)):t.duration;
 t.cycle=int(r.cycle,0,100000,0);t.eyeElapsed=Number.isFinite(r.eyeElapsed)?Math.max(0,Math.min(3600,r.eyeElapsed)):0;
 t.id=text(r.id);t.taskTitle=text(r.taskTitle);t.running=false;
 if(t.phase.endsWith('Done'))t.remaining=0;
 return s;
}
export function tasks(s,date=today()){return s.days[date]||[];}
export function upsertTask(s,date,title,important,urgent,id,dueAt='',lead=15){
 title=text(title);if(!title)throw Error('给任务写个名字吧。');
 const list=s.days[date]||(s.days[date]=[]),old=list.find(x=>x.id===id);
 const due=validDate(dueAt),before=int(lead,0,1440,15);
 if(old){const same=old.dueAt===due&&old.lead===before;Object.assign(old,{title,important:!!important,urgent:!!urgent,dueAt:due,lead:before,reminded:same&&old.reminded});return old;}
 if(list.length>=1000)throw Error('今天的任务已达上限，请先整理一下。');
 const item={id:uid(),title,important:!!important,urgent:!!urgent,done:false,dueAt:due,lead:before,reminded:false};list.push(item);return item;
}
export function reset(s){const t=s.timer;Object.assign(t,{phase:'focus',running:false,duration:s.settings.focusMinutes*60,remaining:s.settings.focusMinutes*60,minutes:s.settings.focusMinutes,id:''});}
export function start(s,title=''){
 if(s.timer.phase==='focusDone'||s.timer.phase==='breakDone')reset(s);
 const t=s.timer;if(t.remaining<=0)reset(s);
 if(t.phase==='focus'&&!t.id){t.id=uid();t.taskTitle=text(title);}
 t.running=true;
}
export function startBreak(s){if(s.timer.phase!=='focusDone')throw Error('先完成一轮专注，再开始休息。');const t=s.timer,minutes=t.cycle%s.settings.cycles===0?s.settings.longBreakMinutes:s.settings.shortBreakMinutes;Object.assign(t,{phase:'break',duration:minutes*60,remaining:minutes*60,running:true,eyeElapsed:0});}
export function tick(s,delta,date=today()){
 const t=s.timer;if(!t.running||!Number.isFinite(delta)||delta<=0)return {};
 if(delta>5){t.running=false;return {gap:true};}
 const used=Math.min(delta,t.remaining);t.remaining=Math.max(0,t.remaining-used);
 if(t.phase==='focus')t.eyeElapsed+=used;
 if(t.remaining<=0){t.running=false;
  if(t.phase==='focus'){
   t.phase='focusDone';let reward=0;
   if(t.id&&!s.records.some(x=>x.id===t.id)){
    reward=t.minutes;s.coins=Math.min(2147483647,s.coins+reward);t.cycle++;
    s.records.push({id:t.id,date,minutes:t.minutes,coins:reward,taskTitle:t.taskTitle});
   }
   return {focusDone:true,reward};
  }t.phase='breakDone';return {breakDone:true};
 }
 if(t.phase==='focus'&&s.settings.eyeEnabled&&t.eyeElapsed>=s.settings.eyeMinutes*60){t.eyeElapsed=0;return {eye:true};}
 return {};
}
export function buy(s,id){const item=ITEMS.find(x=>x.id===id);if(!item)throw Error('没有找到这件物品。');if(!s.owned.includes(id)){if(s.coins<item.price)throw Error('金币还不够，完成一轮专注再来看看吧。');s.coins-=item.price;s.owned.push(id);}s.pet[item.slot]=id;return item;}
export function minutesOn(s,date){return s.records.filter(x=>x.date===date).reduce((n,x)=>n+x.minutes,0);}
export function petName(s){return s.pet[s.pet.kind+'Name'];}
export function upcoming(s){return Object.entries(s.days).flatMap(([date,list])=>list.filter(x=>!x.done&&x.dueAt).map(x=>({...x,date}))).sort((a,b)=>Date.parse(a.dueAt)-Date.parse(b.dueAt));}
export function addAlarm(s,title,at){title=text(title);const date=validDate(at);if(!title||!date)throw Error('请填写闹钟名称和有效时间。');if(s.alarms.length>=200)throw Error('最多保存 200 个闹钟，请先整理已结束的闹钟。');const alarm={id:uid(),title,at:date,enabled:true,notified:false};s.alarms.push(alarm);return alarm;}
export function dueReminders(s,now=Date.now()){
 if(!Number.isFinite(now))return [];const result=[];
 for(const [date,list] of Object.entries(s.days))for(const x of list){if(x.done||!x.dueAt||x.reminded)continue;if(now>=Date.parse(x.dueAt)-x.lead*60000){x.reminded=true;result.push({type:'ddl',title:x.title,at:x.dueAt,date});}}
 for(const x of s.alarms)if(x.enabled&&!x.notified&&now>=Date.parse(x.at)){x.notified=true;x.enabled=false;result.push({type:'alarm',title:x.title,at:x.at});}
 return result;
}
