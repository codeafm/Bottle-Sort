// Рисует скриншоты магазинов из template.html через Edge без окна.
// Запуск: Edge с --remote-debugging-port=9333, затем node render.mjs [порт]
// Результат: output/appstore_6.7 (1290×2796) и output/rustore (1080×1920).
import { mkdirSync, writeFileSync } from 'node:fs'
import { fileURLToPath, pathToFileURL } from 'node:url'
import path from 'node:path'

const here = path.dirname(fileURLToPath(import.meta.url))
const port = process.argv[2] || '9333'
const SIZES = [
  { dir: 'appstore_6.7', w: 1290, h: 2796 },
  { dir: 'rustore', w: 1080, h: 1920 },
]
const COUNT = 8

const target = await (await fetch(`http://127.0.0.1:${port}/json/new?about:blank`, { method: 'PUT' })).json()
const ws = new WebSocket(target.webSocketDebuggerUrl)
await new Promise((r) => ws.addEventListener('open', r))
let id = 0
const pending = new Map()
ws.addEventListener('message', (m) => {
  const msg = JSON.parse(m.data)
  if (msg.id && pending.has(msg.id)) {
    pending.get(msg.id)(msg)
    pending.delete(msg.id)
  }
})
const send = (method, params = {}) =>
  new Promise((resolve) => {
    const n = ++id
    pending.set(n, resolve)
    ws.send(JSON.stringify({ id: n, method, params }))
  })
const sleep = (ms) => new Promise((r) => setTimeout(r, ms))

for (const size of SIZES) {
  mkdirSync(path.join(here, 'output', size.dir), { recursive: true })
  await send('Emulation.setDeviceMetricsOverride', { width: size.w, height: size.h, deviceScaleFactor: 1, mobile: false })
  for (let slide = 1; slide <= COUNT; slide++) {
    const url = `${pathToFileURL(path.join(here, 'template.html')).href}?slide=${slide}&w=${size.w}&h=${size.h}`
    await send('Page.navigate', { url })
    for (let i = 0; i < 40; i++) {
      const r = await send('Runtime.evaluate', { expression: "document.body.dataset.ready === '1' && document.getElementById('shot').complete", returnByValue: true })
      if (r.result?.result?.value) break
      await sleep(150)
    }
    await sleep(300)
    const shot = await send('Page.captureScreenshot', { format: 'png', clip: { x: 0, y: 0, width: size.w, height: size.h, scale: 1 } })
    const file = path.join(here, 'output', size.dir, `${String(slide).padStart(2, '0')}.png`)
    writeFileSync(file, Buffer.from(shot.result.data, 'base64'))
    console.log('saved', path.relative(here, file))
  }
}
await send('Target.closeTarget', { targetId: target.id }).catch(() => {})
process.exit(0)
