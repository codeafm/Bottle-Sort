// Слайды версии 2.0.3 из template_v2.html и кадров source_v2 (их снимает StoreScreenshotCapture в Unity).
// Запуск: Edge с --remote-debugging-port=9333, затем node render_v2.mjs [порт] [номера слайдов через запятую]
// Результат: output_v2/rustore (1080×1920) и output_v2/appstore_6.7 (1290×2796).
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs'
import { fileURLToPath, pathToFileURL } from 'node:url'
import path from 'node:path'

const here = path.dirname(fileURLToPath(import.meta.url))
const port = process.argv[2] || '9333'
const only = process.argv[3] ? process.argv[3].split(',').map(Number) : null
const SIZES = [
  { dir: 'rustore', w: 1080, h: 1920 },
  { dir: 'appstore_6.7', w: 1290, h: 2796 },
]
const COUNT = 6

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
  mkdirSync(path.join(here, 'output_v2', size.dir), { recursive: true })
  const marks = JSON.parse(readFileSync(path.join(here, 'source_v2', size.dir, 'marks.json'), 'utf8'))
  await send('Emulation.setDeviceMetricsOverride', { width: size.w, height: size.h, deviceScaleFactor: 1, mobile: false })
  for (let slide = 1; slide <= COUNT; slide++) {
    if (only && !only.includes(slide)) continue
    const shot = String(slide).padStart(2, '0')
    const query = new URLSearchParams({ slide, w: size.w, h: size.h, dir: size.dir, marks: JSON.stringify(marks[shot] || {}) })
    await send('Page.navigate', { url: `${pathToFileURL(path.join(here, 'template_v2.html')).href}?${query}` })
    for (let i = 0; i < 60; i++) {
      const r = await send('Runtime.evaluate', { expression: "document.body.dataset.ready === '1'", returnByValue: true })
      if (r.result?.result?.value) break
      await sleep(150)
    }
    await sleep(300)
    const png = await send('Page.captureScreenshot', { format: 'png', clip: { x: 0, y: 0, width: size.w, height: size.h, scale: 1 } })
    const file = path.join(here, 'output_v2', size.dir, `${shot}.png`)
    writeFileSync(file, Buffer.from(png.result.data, 'base64'))
    console.log('saved', path.relative(here, file))
  }
}
await send('Target.closeTarget', { targetId: target.id }).catch(() => {})
process.exit(0)
