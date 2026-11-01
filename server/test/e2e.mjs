// Uçtan uca test: gerçek Nakama sunucusuna WebSocket ile bağlanan sahte oyuncular yarıştırır.
// Önkoşul: sunucu test ayarlarıyla çalışıyor olmalı (bkz. docs/PROTOCOL.md "Test"):
//   TEXTRUN_LOBBY_WAIT_MS=3000 TEXTRUN_WORD_COUNT=8 TEXTRUN_MAX_WPM=100000 TEXTRUN_FINISH_GRACE_MS=3000 TEXTRUN_COUNTDOWN_MS=1500
// Kullanım: node test/e2e.mjs
import assert from 'node:assert/strict';

const HOST = '127.0.0.1:7350';
const OP = { SUBMIT: 1, START: 2, SNAPSHOT: 100, ROSTER: 101, COUNTDOWN: 102, GO: 103, PROGRESS: 104, ACK: 105, RESULTS: 106, ENDING: 107 };
const runId = Date.now().toString(36);
// Sunucuda TEXTRUN_FINISH_GRACE_MS verilmediyse varsayilan (10 sn) beklenir: E2E_EXPECT_GRACE_MS=10000
const expectedGraceMs = Number(process.env.E2E_EXPECT_GRACE_MS || 3000);

let failures = 0;
async function test(name, fn) {
  if (process.env.E2E_CHEAT && !name.startsWith('hile')) return; // hile modunda yalnızca hile testi çalışır
  const started = Date.now();
  try {
    await fn();
    console.log(`[OK]   ${name} (${Date.now() - started} ms)`);
  } catch (e) {
    failures++;
    console.log(`[FAIL] ${name}\n       ${e.stack.split('\n').slice(0, 3).join('\n       ')}`);
  }
}

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

async function post(path, headers, body) {
  const res = await fetch(`http://${HOST}${path}`, { method: 'POST', headers: { 'Content-Type': 'application/json', ...headers }, body });
  const json = await res.json();
  if (!res.ok) throw new Error(`${path}: ${JSON.stringify(json)}`);
  return json;
}

class Player {
  constructor(name, lang) {
    this.name = name;
    this.lang = lang;
    this.events = [];
    this.waiters = [];
    this.cid = 0;
    this.pending = new Map();
  }

  async login() {
    const basic = Buffer.from('defaultkey:').toString('base64');
    const auth = await post(`/v2/account/authenticate/device?create=true&username=${this.name}`, { Authorization: `Basic ${basic}` }, JSON.stringify({ id: `e2e-${runId}-${this.name}` }));
    this.token = auth.token;
    this.userId = JSON.parse(Buffer.from(auth.token.split('.')[1], 'base64url').toString()).uid;
    return this;
  }

  // Kullanıcı adı + şifre ile kalıcı hesap (istemcideki gibi: giriş adı -> sahte e-posta)
  async loginAccount(loginName, password) {
    const basic = Buffer.from('defaultkey:').toString('base64');
    const auth = await post(`/v2/account/authenticate/email?create=true&username=${loginName}`, { Authorization: `Basic ${basic}` }, JSON.stringify({ email: `${loginName}@textrun.local`, password }));
    this.token = auth.token;
    this.userId = JSON.parse(Buffer.from(auth.token.split('.')[1], 'base64url').toString()).uid;
    return this;
  }

  async connect() {
    this.ws = new WebSocket(`ws://${HOST}/ws?token=${this.token}&format=json`);
    this.ws.onmessage = (m) => this.onMessage(JSON.parse(m.data));
    await new Promise((resolve, reject) => {
      this.ws.onopen = resolve;
      this.ws.onerror = () => reject(new Error('websocket baglanamadi'));
    });
    return this;
  }

  onMessage(msg) {
    if (msg.cid && this.pending.has(msg.cid)) {
      this.pending.get(msg.cid)(msg);
      this.pending.delete(msg.cid);
    } else if (msg.match_data) {
      const ev = { op: Number(msg.match_data.op_code), body: JSON.parse(Buffer.from(msg.match_data.data || '', 'base64').toString() || 'null'), at: Date.now() };
      this.events.push(ev);
      this.waiters = this.waiters.filter((w) => !w(ev));
    }
  }

  request(payload) {
    const cid = String(++this.cid);
    return new Promise((resolve) => {
      this.pending.set(cid, resolve);
      this.ws.send(JSON.stringify({ cid, ...payload }));
    });
  }

  async rpc(id, body) {
    const res = await post(`/v2/rpc/${id}`, { Authorization: `Bearer ${this.token}` }, JSON.stringify(JSON.stringify(body)));
    return JSON.parse(res.payload);
  }

  async findAndJoin() {
    const found = await this.rpc('find_race', { lang: this.lang });
    return this.join(found.matchId, this.lang);
  }

  async join(matchId, langMeta) {
    this.matchId = matchId;
    const res = await this.request({ match_join: { match_id: matchId, metadata: { lang: langMeta } } });
    return res;
  }

  async joinWith(matchId, metadata) {
    this.matchId = matchId;
    return this.request({ match_join: { match_id: matchId, metadata } });
  }

  start(bots) {
    const data = Buffer.from(JSON.stringify({ bots })).toString('base64');
    this.ws.send(JSON.stringify({ match_data_send: { match_id: this.matchId, op_code: String(OP.START), data } }));
  }

  waitFor(op, timeoutMs = 20000) {
    const existing = this.events.find((e) => e.op === op);
    if (existing) return Promise.resolve(existing);
    return new Promise((resolve, reject) => {
      const timer = setTimeout(() => reject(new Error(`${this.name}: op ${op} ${timeoutMs} ms icinde gelmedi`)), timeoutMs);
      this.waiters.push((ev) => {
        if (ev.op !== op) return false;
        clearTimeout(timer);
        resolve(ev);
        return true;
      });
    });
  }

  submit(word) {
    const data = Buffer.from(JSON.stringify({ w: word })).toString('base64');
    this.ws.send(JSON.stringify({ match_data_send: { match_id: this.matchId, op_code: String(OP.SUBMIT), data } }));
  }

  // Tüm kelimeleri sırayla yazar. Her kelime için sunucunun ACK'ını bekler.
  async typeAll(words, delayMs = 30) {
    for (const w of words) {
      const before = this.events.filter((e) => e.op === OP.ACK).length;
      this.submit(w);
      for (let i = 0; i < 100 && this.events.filter((e) => e.op === OP.ACK).length === before; i++) await sleep(20);
      await sleep(delayMs);
    }
  }

  close() {
    this.ws?.close();
  }
}

async function makePlayers(prefix, lang, count) {
  const players = [];
  for (let i = 1; i <= count; i++) players.push(await new Player(`${prefix}${i}${runId}`, lang).login());
  for (const p of players) await p.connect();
  return players;
}

await test('sunucu ayakta ve ozel fonksiyon calisiyor', async () => {
  const [p] = await makePlayers('ping', 'tr', 1);
  const res = await p.rpc('ping', {});
  assert.equal(res.ok, true);
  p.close();
});

await test('5 Turkce + 5 Ingilizce oyuncu: diller asla karismaz, herkes yarisi bitirir', async () => {
  const tr = await makePlayers('tr', 'tr', 5);
  const en = await makePlayers('en', 'en', 5);

  // Arka arkaya (gerçek akıştaki gibi) yarış bulup katıl; TR ve EN sırayla karışık gelir
  for (let i = 0; i < 5; i++) {
    assert.ok(!(await tr[i].findAndJoin()).error, 'tr katilim');
    assert.ok(!(await en[i].findAndJoin()).error, 'en katilim');
  }

  assert.equal(new Set(tr.map((p) => p.matchId)).size, 1, '5 Turkce oyuncu ayni odada olmali');
  assert.equal(new Set(en.map((p) => p.matchId)).size, 1, '5 Ingilizce oyuncu ayni odada olmali');
  assert.notEqual(tr[0].matchId, en[0].matchId, 'Turkce ve Ingilizce farkli odada olmali');

  const trStart = await Promise.all(tr.map((p) => p.waitFor(OP.COUNTDOWN)));
  const enStart = await Promise.all(en.map((p) => p.waitFor(OP.COUNTDOWN)));
  assert.equal(trStart[0].body.lang, 'tr');
  assert.equal(enStart[0].body.lang, 'en');

  // Herkese AYNI kelime listesi gitmeli
  const trWords = trStart.map((e) => JSON.stringify(e.body.words));
  assert.equal(new Set(trWords).size, 1, 'Turkce oyuncularin listesi ayni olmali');
  assert.equal(trStart[0].body.words.length, 8);
  assert.equal(trStart[0].body.players.filter((x) => x.isBot).length, 0, '5 insan varken bot olmamali');
  assert.ok(enStart[0].body.words.every((w) => /^[a-z]+$/.test(w)), 'Ingilizce kelimelerde Turkce harf olmamali');

  await Promise.all([...tr, ...en].map((p) => p.waitFor(OP.GO)));

  // Oyuncular farklı hızlarda yazar: tr[0] en hızlı, tr[4] en yavaş
  await Promise.all([
    ...tr.map((p, i) => p.typeAll(trStart[0].body.words, 20 + i * 60)),
    ...en.map((p, i) => p.typeAll(enStart[0].body.words, 20 + i * 60)),
  ]);

  const trRes = await Promise.all(tr.map((p) => p.waitFor(OP.RESULTS)));
  const enRes = await Promise.all(en.map((p) => p.waitFor(OP.RESULTS)));

  for (const [res, group] of [[trRes, tr], [enRes, en]]) {
    const results = res[0].body.results;
    assert.equal(results.length, 5);
    assert.deepEqual(results.map((r) => r.rank), [1, 2, 3, 4, 5]);
    assert.ok(results.every((r) => r.status === 'finished' && r.accuracy === 1), 'hepsi hatasiz bitirmeli');
    assert.ok(results.every((r) => !r.isBot));
    assert.equal(results[0].userId, group[0].userId, 'en hizli yazan birinci olmali');
    assert.equal(results[4].userId, group[4].userId, 'en yavas yazan besinci olmali');
    assert.equal(JSON.stringify(res[0].body.results), JSON.stringify(res[4].body.results), 'herkese ayni sonuc gitmeli');
  }

  [...tr, ...en].forEach((p) => p.close());
});

await test('yanlis dil bilgisiyle odaya girilemez (dil kurali sunucuda zorunlu)', async () => {
  const [turk, ingiliz] = [await new Player(`kural-tr${runId}`, 'tr').login(), await new Player(`kural-en${runId}`, 'en').login()];
  await turk.connect();
  await ingiliz.connect();

  const found = await turk.rpc('find_race', { lang: 'tr' });
  const wrong = await ingiliz.join(found.matchId, 'en');
  assert.ok(wrong.error, 'Ingilizce oyuncu Turkce odaya girememeli');
  assert.match(JSON.stringify(wrong.error), /language_mismatch/);

  const noMeta = await ingiliz.request({ match_join: { match_id: found.matchId } });
  assert.ok(noMeta.error, 'dil bilgisi olmadan girilememeli');

  const ok = await turk.join(found.matchId, 'tr');
  assert.ok(!ok.error, 'dogru dil girebilmeli');

  // Ingilizce oyuncunun yarış araması Türkçe odaya düşmemeli
  const enFound = await ingiliz.rpc('find_race', { lang: 'en' });
  assert.notEqual(enFound.matchId, found.matchId);

  await assert.rejects(() => ingiliz.rpc('find_race', { lang: 'fr' }), /desteklenmeyen dil/);
  turk.close();
  ingiliz.close();
});

await test('tek oyuncu: sure dolunca 4 bot gelir, yanlis kelime ilerletmez, erken gonderim yok sayilir', async () => {
  const [p] = await makePlayers('solo', 'tr', 1);
  const join = await p.findAndJoin();
  assert.ok(!join.error);

  const countdown = await p.waitFor(OP.COUNTDOWN, 10000);
  const players = countdown.body.players;
  assert.equal(players.length, 5);
  assert.equal(players.filter((x) => x.isBot).length, 4, '4 bot olmali');
  assert.ok(players.filter((x) => x.isBot).every((x) => x.name.startsWith('Bot ')), 'botlar acikca Bot diye etiketli');

  // Geri sayımdayken gönderilen kelime kabul edilmemeli (ACK gelmez)
  p.submit(countdown.body.words[0]);
  await sleep(300);
  assert.equal(p.events.filter((e) => e.op === OP.ACK).length, 0, 'baslamadan gonderim yok sayilmali');

  await p.waitFor(OP.GO);
  p.submit('kesinlikleyanlis');
  const wrongAck = await p.waitFor(OP.ACK);
  assert.equal(wrongAck.body.ok, false);
  assert.equal(wrongAck.body.index, 0, 'yanlis kelime ilerletmemeli');

  await p.typeAll(countdown.body.words, 20);
  const res = await p.waitFor(OP.RESULTS);
  const results = res.body.results;
  const me = results.find((r) => r.userId === p.userId);

  // Yeni kural: ilk bitirenden sonra geri sayim (test ayarinda 3 sn); sonuc bitirir bitirmez degil, sure dolunca gelir
  const ending = p.events.find((e) => e.op === OP.ENDING);
  assert.ok(ending, 'ilk bitirenden sonra ENDING (geri sayim) mesaji gelmeli');
  const countdownMs = ending.body.endsAt - ending.body.serverNow;
  assert.ok(Math.abs(countdownMs - expectedGraceMs) <= 500, 'geri sayim suresi ~' + expectedGraceMs + ' ms olmali (' + countdownMs + ')');
  assert.ok(res.at - ending.at >= expectedGraceMs - 1000, 'sonuclar geri sayim dolunca gelmeli: ' + (res.at - ending.at) + ' ms');
  assert.ok(results.filter((r) => r.isBot).some((r) => r.correctWords > 0), 'botlar geri sayim boyunca ilerlemeli');

  assert.equal(me.rank, 1, 'cok hizli yazan insan botlari geride birakmali');
  assert.equal(me.status, 'finished');
  assert.equal(me.errors, 1);
  assert.equal(results.filter((r) => r.isBot).length, 4);
  p.close();
});

await test('baglanti kopup geri gelen oyuncu yarisa devam eder', async () => {
  const [p] = await makePlayers('geri', 'en', 1);
  await p.findAndJoin();
  const countdown = await p.waitFor(OP.COUNTDOWN, 10000);
  await p.waitFor(OP.GO);
  await p.typeAll(countdown.body.words.slice(0, 3), 20);
  const matchId = p.matchId;
  const userToken = p.token;
  p.close();
  await sleep(500);

  const again = new Player('geri-yeni', 'en');
  again.token = userToken;
  again.userId = p.userId;
  await again.connect();
  const join = await again.join(matchId, 'en');
  assert.ok(!join.error, 'ayni oyuncu geri girebilmeli: ' + JSON.stringify(join.error));

  const snap = await again.waitFor(OP.SNAPSHOT);
  assert.equal(snap.body.phase, 'racing');
  const me = snap.body.players.find((x) => x.id === p.userId);
  assert.equal(me.index, 3, 'ilerleme korunmali');

  await again.typeAll(countdown.body.words.slice(3), 20);
  const res = await again.waitFor(OP.RESULTS);
  assert.equal(res.body.results.find((r) => r.userId === p.userId).status, 'finished');
  again.close();
});

await test('eszamanli yuk: 12 oyuncu ayni anda aranir, hicbir oda 5 insani asmaz, herkes yerlesir', async () => {
  const players = await makePlayers('yuk', 'tr', 12);

  // Oda doluysa sunucu reddeder; istemci yeniden arar (gerçek istemcinin yapacağı gibi)
  async function joinWithRetry(p) {
    for (let attempt = 0; attempt < 6; attempt++) {
      const res = await p.findAndJoin();
      if (!res.error) return;
      await sleep(150);
    }
    throw new Error(p.name + ' hicbir odaya yerlesemedi');
  }
  await Promise.all(players.map(joinWithRetry));

  const rooms = new Map();
  for (const p of players) rooms.set(p.matchId, (rooms.get(p.matchId) || 0) + 1);
  const sizes = [...rooms.values()].sort((a, b) => b - a);
  assert.ok(sizes.every((s) => s <= 5), 'oda boyutlari: ' + sizes);
  assert.ok(rooms.size <= 4, 'en fazla 4 oda olmali, ' + rooms.size + ' acildi: ' + sizes);
  assert.equal(sizes.reduce((a, b) => a + b, 0), 12);
  console.log(`       12 oyuncu ${rooms.size} odaya yerlesti: ${sizes.join(', ')}`);
  players.forEach((p) => p.close());
});

await test('ozel oda: kod ile katilinir, yanlis kod/yanlis oda reddedilir, kurucu baslatir (botsuz)', async () => {
  const [host, friend, stranger] = await makePlayers('oda', 'tr', 3);

  const room = await host.rpc('create_room', { lang: 'tr' });
  assert.match(room.code, /^[A-HJKMNP-Z2-9]{5}$/, 'kod 5 karakter, karisan harf yok');
  const hostJoin = await host.joinWith(room.matchId, { lang: room.lang, code: room.code });
  assert.ok(hostJoin.match, 'kurucu odaya girdi');
  const snap = await host.waitFor(OP.SNAPSHOT);
  assert.equal(snap.body.kind, 'private');
  assert.equal(snap.body.code, room.code);
  assert.equal(snap.body.host, host.userId);

  // Oda numarasini bilen ama kodu bilmeyen giremez
  const noCode = await stranger.joinWith(room.matchId, { lang: 'tr' });
  assert.ok(noCode.error, 'kodsuz giris reddedilmeli');
  const wrongCode = await stranger.joinWith(room.matchId, { lang: 'tr', code: 'ZZZZZ' });
  assert.ok(wrongCode.error, 'yanlis kodla giris reddedilmeli');

  // Kod ile arayan arkadas bulur (kucuk harf/bosluk onemsiz)
  const found = await friend.rpc('join_room', { code: ' ' + room.code.toLowerCase() + ' ' });
  assert.equal(found.matchId, room.matchId);
  assert.equal(found.lang, 'tr');
  const friendJoin = await friend.joinWith(found.matchId, { lang: found.lang, code: room.code });
  assert.ok(friendJoin.match, 'arkadas odaya girdi');

  // Var olmayan kod
  await assert.rejects(() => stranger.rpc('join_room', { code: 'AAAAA' }), /room_not_found/);

  // Kurucu olmayan baslatamaz: kisa sure beklenip geri sayim gelmedigi gorulur
  friend.start(false);
  await sleep(1500);
  assert.ok(!host.events.some((e) => e.op === OP.COUNTDOWN), 'kurucu olmayanin baslat istegi yok sayilir');

  host.start(false);
  const countdown = await host.waitFor(OP.COUNTDOWN, 8000);
  assert.equal(countdown.body.players.length, 2, 'botsuz: yalnizca 2 insan');
  assert.ok(countdown.body.players.every((p) => !p.isBot));

  // Baslayan odaya yeni katilinamaz
  await assert.rejects(() => stranger.rpc('join_room', { code: room.code }), /room_closed/);

  await host.waitFor(OP.GO);
  await Promise.all([host.typeAll(countdown.body.words), friend.typeAll(countdown.body.words)]);
  const res = await host.waitFor(OP.RESULTS, 20000);
  assert.equal(res.body.results.length, 2);
  assert.ok(res.body.results.every((r) => r.status === 'finished'));
  for (const p of [host, friend, stranger]) p.close();
});

await test('ozel oda: botlu baslatinca bosluklar dolar; ayni dil kurali (yanlis dil reddedilir)', async () => {
  const [host, wrongLang] = await makePlayers('odab', 'tr', 2);
  const room = await host.rpc('create_room', { lang: 'tr' });
  await host.joinWith(room.matchId, { lang: 'tr', code: room.code });
  const bad = await wrongLang.joinWith(room.matchId, { lang: 'en', code: room.code });
  assert.ok(bad.error, 'dil uyusmazligi reddedilir');

  host.start(true);
  const countdown = await host.waitFor(OP.COUNTDOWN, 8000);
  assert.equal(countdown.body.players.length, 5);
  assert.equal(countdown.body.players.filter((p) => p.isBot).length, 4);
  host.close();
  wrongLang.close();
});

await test('profil: ad secilir, yarista gorunur, yarisi bitirince XP verilir ve kaydedilir', async () => {
  const p = new Player(`prof1${runId}`, 'en');
  await p.loginAccount(`prof1${runId}`, 'sifre12345');
  await p.connect();
  const fresh = await p.rpc('get_profile', {});
  assert.equal(fresh.guest, false);
  assert.equal(fresh.name, null, 'yeni hesapta ad yok');
  assert.equal(fresh.xp, 0);
  assert.equal(fresh.level, 1);

  await assert.rejects(() => p.rpc('set_name', { name: 'a' }), /name_invalid/, 'cok kisa ad reddedilir');
  await assert.rejects(() => p.rpc('set_name', { name: '<b>x</b>' }), /name_invalid/, 'yasak karakter reddedilir');
  const named = await p.rpc('set_name', { name: '  Hizli   Ali ' });
  assert.equal(named.name, 'Hizli Ali');

  await p.findAndJoin();
  const countdown = await p.waitFor(OP.COUNTDOWN, 10000);
  assert.ok(countdown.body.players.some((x) => x.name === 'Hizli Ali'), 'yarista secilen ad gorunur');
  await p.waitFor(OP.GO);
  await p.typeAll(countdown.body.words, 120);
  const res = await p.waitFor(OP.RESULTS, 20000);
  const me = res.body.results.find((r) => r.userId === p.userId);
  const reward = res.body.rewards.find((r) => r.userId === p.userId);
  assert.ok(reward && reward.gain >= 20, 'bitiren XP kazanir');
  assert.equal(reward.gain, 20 + [50, 35, 25, 15, 10][me.rank - 1]);

  const after = await p.rpc('get_profile', {});
  assert.equal(after.xp, reward.gain);
  assert.equal(after.races, 1);
  assert.equal(after.name, 'Hizli Ali');
  p.close();
});

await test('misafir: takma ad secemez, yarisi bitirse de XP/seviye kazanmaz', async () => {
  const [g] = await makePlayers('misafir', 'en', 1);
  const profile = await g.rpc('get_profile', {});
  assert.equal(profile.guest, true);
  assert.equal(profile.level, 1);
  await assert.rejects(() => g.rpc('set_name', { name: 'Ahmet' }), /guest_account/, 'misafir ad secemez');

  await g.findAndJoin();
  const countdown = await g.waitFor(OP.COUNTDOWN, 10000);
  await g.waitFor(OP.GO);
  await g.typeAll(countdown.body.words, 120);
  const res = await g.waitFor(OP.RESULTS, 20000);
  assert.equal(res.body.rewards.length, 0, 'misafire odul verilmez');
  const after = await g.rpc('get_profile', {});
  assert.equal(after.xp, 0);
  assert.equal(after.races, 0);
  g.close();
});

// Hile testi yalnızca sunucu varsayılan hız sınırıyla (250 WPM) çalışırken anlamlıdır: E2E_CHEAT=1
if (process.env.E2E_CHEAT) {
  await test('hile: insan icin imkansiz hizda yazan oyuncu diskalifiye edilir', async () => {
    const [p] = await makePlayers('hile', 'en', 1);
    await p.findAndJoin();
    const countdown = await p.waitFor(OP.COUNTDOWN, 10000);
    await p.waitFor(OP.GO);
    for (const w of countdown.body.words) p.submit(w); // hepsi bir anda
    const res = await p.waitFor(OP.RESULTS);
    const me = res.body.results.find((r) => r.userId === p.userId);
    assert.equal(me.status, 'disqualified');
    assert.equal(me.rank, 5, 'diskalifiye en sona gider');
    assert.ok(p.events.some((e) => e.op === OP.ACK && e.body.kind === 'disqualified'));
    p.close();
  });
}

console.log(failures === 0 ? '\nTUM UCTAN UCA TESTLER GECTI' : `\n${failures} TEST BASARISIZ`);
process.exit(failures === 0 ? 0 : 1);
