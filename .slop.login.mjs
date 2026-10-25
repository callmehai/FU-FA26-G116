// sloplint --setup module for TrustCabinet (see the sloplint skill and AGENTS.md).
//
// Before each page loads: signs in as a seeded development user, the admin for /mgmt/admin/* and
// the secretary elsewhere (SLOP_SEED=chutoa picks another). /login and /reset-password stay signed
// out. The demo password is the public one in DevelopmentDataSeeder.cs, read from there.
//
// After the page loads, the URL hash says what to open, so a scan reaches what needs a click:
//   /mgmt/meetings/<id>#tab=Tài liệu      the detail tab with that label
//   /mgmt/meetings#open=notifications     the notification panel
//   /mgmt/meetings#click=Tạo cuộc họp     the button with that name (a dialog: sloplint presses
//                                         Escape after load, which closes one opened from ?create=1)
import { readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'

const seeder = fileURLToPath(new URL('./backend/TrustCabinet.Infrastructure/Seed/DevelopmentDataSeeder.cs', import.meta.url))
const PASSWORD = readFileSync(seeder, 'utf8').match(/DemoPassword = "([^"]+)"/)[1]
const SIGNED_OUT = ['/login', '/reset-password']

export default async ({ context, url }) => {
  const { origin, pathname } = new URL(url)
  if (SIGNED_OUT.some((p) => pathname.startsWith(p))) return
  const seed = process.env.SLOP_SEED ?? (pathname.startsWith('/mgmt/admin') ? 'quantri' : 'thuky')
  // The API sets the refresh cookie; the flag tells the SPA to restore the session from it.
  const res = await context.request.post(`${origin}/api/auth/login`, { data: { email: `${seed}@trustcabinet.local`, password: PASSWORD } })
  if (!res.ok()) throw new Error(`sign-in as ${seed} failed: HTTP ${res.status()}`)
  await context.addInitScript(() => localStorage.setItem('trustcabinet.signedIn', '1'))
}

export async function ready({ page, url }) {
  const hash = new URLSearchParams(new URL(url).hash.slice(1))
  if (hash.get('tab')) await page.getByRole('tab', { name: hash.get('tab'), exact: true }).click()
  if (hash.get('open') === 'notifications') await page.locator('.notif-bell').click()
  const name = hash.get('click')
  if (name) await page.getByRole('button', { name, exact: true }).or(page.getByRole('link', { name, exact: true })).first().click()
}
