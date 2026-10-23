// sloplint --setup hook: signs each page in as a seeded development user before it loads.
// /mgmt/admin/* needs the admin; everything else the secretary. SLOP_SEED overrides (e.g. chutoa).
// The demo password is the public one in DevelopmentDataSeeder.cs, read from there so it is kept
// in one place.
import { readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'

const seeder = fileURLToPath(new URL('../../../backend/TrustCabinet.Infrastructure/Seed/DevelopmentDataSeeder.cs', import.meta.url))
const PASSWORD = readFileSync(seeder, 'utf8').match(/DemoPassword = "([^"]+)"/)[1]

export default async ({ context, url }) => {
  const { origin, pathname } = new URL(url)
  const seed = process.env.SLOP_SEED ?? (pathname.startsWith('/mgmt/admin') ? 'quantri' : 'thuky')
  // The API sets the refresh cookie; the flag tells the SPA to restore the session from it.
  const res = await context.request.post(`${origin}/api/auth/login`, { data: { email: `${seed}@trustcabinet.local`, password: PASSWORD } })
  if (!res.ok()) throw new Error(`sign-in as ${seed} failed: HTTP ${res.status()}`)
  await context.addInitScript(() => localStorage.setItem('trustcabinet.signedIn', '1'))
}
