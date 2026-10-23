// The anti-ai-slop skill's rendered-page check. One arrow function, no imports, so it runs anywhere
// a page can evaluate a function:
//   Playwright:      await page.evaluate(`(${readFileSync(SKILL_DIR + '/slop-check.js', 'utf8')})()`)
//   chrome-devtools: evaluate_script with the file's text as `function`
// It reads computed styles of what is visible, like Adrian Krebs' design-slop study
// (adriankrebs.ch/blog/design-slop): deterministic DOM checks, no model judging a screenshot.
// Each key is one tell from SKILL.md; an empty list means the tell is absent.
() => {
  const visible = (e) => {
    const r = e.getBoundingClientRect()
    const s = getComputedStyle(e)
    return r.width > 0 && r.height > 8 && s.visibility !== 'hidden' && s.display !== 'none' && r.bottom > 0
  }
  const rgba = (c) => (c.match(/[\d.]+/g) ?? []).map(Number)
  const transparent = (c) => c === 'transparent' || (rgba(c).length === 4 && rgba(c)[3] === 0)
  // Saturation of an rgb() colour, 0..1; greys and black score near 0.
  const saturation = (c) => {
    const [r, g, b] = rgba(c).map((v) => v / 255)
    const max = Math.max(r, g, b)
    const min = Math.min(r, g, b)
    if (max === min) return 0
    const l = (max + min) / 2
    return (max - min) / (1 - Math.abs(2 * l - 1))
  }
  const hue = (c) => {
    const [r, g, b] = rgba(c)
    const max = Math.max(r, g, b)
    const d = max - Math.min(r, g, b)
    if (d === 0) return null
    const h = max === r ? ((g - b) / d) % 6 : max === g ? (b - r) / d + 2 : (r - g) / d + 4
    return Math.round((h * 60 + 360) % 360)
  }
  const painted = (s) => !transparent(s.backgroundColor) || s.borderTopWidth !== '0px'
  const text = (e) => (e.textContent ?? '').trim().replace(/\s+/g, ' ')
  const label = (e) => `${e.tagName.toLowerCase()}.${[...e.classList].slice(0, 3).join('.')} «${text(e).slice(0, 36)}»`
  const all = [...document.querySelectorAll('body *')].filter(visible)
  const main = document.querySelector('main') ?? document.body
  const words = main.innerText

  // T1 Decorative gradients: any gradient fill except loading skeletons and ruled grid lines.
  const gradients = all
    .filter((e) => {
      const bg = getComputedStyle(e).backgroundImage
      return bg.includes('gradient') && !bg.startsWith('repeating-') && !e.matches('.skeleton, [aria-busy="true"] *')
    })
    .map(label)

  // T2 Gradient text.
  const gradientText = all
    .filter((e) => {
      const s = getComputedStyle(e)
      return s.backgroundClip === 'text' || s.webkitBackgroundClip === 'text'
    })
    .map(label)

  // T3 Glow: a saturated, blurred shadow (a coloured halo), or any text-shadow. Focus rings are spread-only.
  const glow = all
    .filter((e) => {
      const s = getComputedStyle(e)
      if (s.textShadow !== 'none') return true
      return s.boxShadow
        .split(/,(?![^(]*\))/)
        .some((part) => {
          const color = part.match(/rgba?\([^)]*\)/)?.[0]
          const blur = Number(part.replace(/rgba?\([^)]*\)/, '').trim().split(/\s+/)[2]?.replace('px', '') ?? 0)
          return !part.includes('inset') && color && blur >= 8 && saturation(color) > 0.35 && (rgba(color)[3] ?? 1) > 0.15
        })
    })
    .map(label)

  // T4 Glass: blur behind a translucent surface.
  const glass = all.filter((e) => getComputedStyle(e).backdropFilter.includes('blur')).map(label)

  // T5 Eyebrows and shouting labels: uppercase with tracking, on anything short.
  const caps = all
    .filter((e) => {
      const s = getComputedStyle(e)
      return s.textTransform === 'uppercase' && parseFloat(s.letterSpacing) > 0 && text(e).length > 0
    })
    .map(label)

  // T6 Side stripe: one edge 3px+ in a colour, on a box whose other edges are thin.
  const stripes = all
    .filter((e) => {
      const s = getComputedStyle(e)
      const sides = ['Left', 'Right', 'Top', 'Bottom'].map((k) => [parseFloat(s[`border${k}Width`]), s[`border${k}Color`]])
      const thick = sides.filter(([w, c]) => w >= 3 && saturation(c) > 0.25)
      const insetStripe = /inset\s+(-?[3-9]px\s+0px|0px\s+-?[3-9]px)\s+0px\s+0px/.test(s.boxShadow)
      return (thick.length === 1 && sides.filter(([w]) => w >= 3).length === 1) || insetStripe
    })
    .map(label)

  // T7 Pills: a painted box, wider than tall, with corners half its height. Inputs and avatars excluded.
  const pills = all
    .filter((e) => {
      const r = e.getBoundingClientRect()
      const s = getComputedStyle(e)
      return (
        painted(s) &&
        r.height <= 44 &&
        r.width > r.height * 1.3 &&
        parseFloat(s.borderTopLeftRadius) >= r.height / 2 - 1 &&
        !e.matches('input, textarea')
      )
    })
    .map(label)
  const pillButtons = pills.filter((p) => /^(button|a)\./.test(p) || p.includes('.btn'))

  // T8 Endless motion: infinite animations (ping, pulse, nudge, shimmer outside a skeleton).
  const endless = all
    .filter((e) => {
      const s = getComputedStyle(e)
      return s.animationName !== 'none' && s.animationIterationCount === 'infinite' && !e.closest('.skeleton')
    })
    .map((e) => `${getComputedStyle(e).animationName} ${label(e)}`)

  // T9 Icon in a tinted tile: a non-interactive box whose only child is an icon, on a tint.
  const iconTiles = all
    .filter((e) => {
      const r = e.getBoundingClientRect()
      const s = getComputedStyle(e)
      return (
        r.width >= 24 &&
        r.width <= 64 &&
        Math.abs(r.width - r.height) <= 2 &&
        e.children.length === 1 &&
        e.firstElementChild?.tagName.toLowerCase() === 'svg' &&
        text(e) === '' &&
        !transparent(s.backgroundColor) &&
        // Avatars and the product's own mark are identity, not decoration.
        !e.matches('button, a, [role="button"], .avatar, [class*="avatar"], [class*="brand"], [class*="logo"]')
      )
    })
    .map(label)

  // T10 Icon soup: a small non-interactive chip carrying an icon AND words AND a tint.
  const iconSoup = all
    .filter((e) => {
      const r = e.getBoundingClientRect()
      return (
        r.height <= 30 &&
        e.querySelector(':scope > svg') &&
        text(e).length > 0 &&
        painted(getComputedStyle(e)) &&
        !e.matches('button, a, .btn, [role="button"], [role="tab"], [role="option"]')
      )
    })
    .map(label)

  // T11 KPI tile row: 3+ siblings of one shape, each a card with a big number.
  const kpiRows = [...new Set(all.map((e) => e.parentElement))]
    .filter((p) => {
      const kids = [...(p?.children ?? [])].filter(visible)
      if (kids.length < 3 || kids.length > 6) return false
      const shape = (k) => `${k.tagName}.${k.classList[0] ?? ''}`
      if (new Set(kids.map(shape)).size !== 1) return false
      return kids.every((k) => {
        const s = getComputedStyle(k)
        const big = [...k.querySelectorAll('*')].some(
          (n) => /^\s*[\d.,]+\s*%?\s*$/.test(n.textContent ?? '') && parseFloat(getComputedStyle(n).fontSize) >= 20,
        )
        return big && (painted(s) || s.boxShadow !== 'none')
      })
    })
    .map((p) => `${label(p)} ×${p.children.length}`)

  // T12 Emoji and sparkles standing in for icons.
  const emoji = (words.match(/\p{Extended_Pictographic}/gu) ?? []).concat(
    [...document.querySelectorAll('svg.lucide-sparkles, svg.lucide-sparkle, svg.lucide-wand-sparkles')].map(() => 'sparkles-icon'),
  )

  // T13 Template meta strings: "A · B · C" joins and arrows glued to labels in visible text.
  const metaStrings = [...main.querySelectorAll('*')]
    .filter((e) => visible(e) && [...e.childNodes].some((n) => n.nodeType === 3 && /\s·\s|\s→$|^→\s/.test(n.textContent ?? '')))
    .map(label)

  // T14 Empty values drawn as a dash instead of words.
  const dashCells = all
    .filter((e) => e.children.length === 0 && /^[—–-]$/.test(text(e)))
    .map(label)

  // T15 Text under 12px (ADR-0012 floor).
  const tiny = all
    .filter((e) => {
      const own = [...e.childNodes].some((n) => n.nodeType === 3 && n.textContent.trim())
      return own && parseFloat(getComputedStyle(e).fontSize) < 12
    })
    .map(label)

  // T16 One radius on everything: share of rounded boxes that use the most common radius.
  const radii = all
    .map((e) => getComputedStyle(e).borderTopLeftRadius)
    .filter((r) => r !== '0px' && !r.includes('%') && parseFloat(r) < 100)
  const counts = radii.reduce((m, r) => m.set(r, (m.get(r) ?? 0) + 1), new Map())
  const [topRadius, topCount] = [...counts].sort((a, b) => b[1] - a[1])[0] ?? ['0px', 0]

  // T17 Shadows doing a border's job: resting boxes with both a border and a drop shadow. Focus rings
  // (no offset, no blur) and overlays (dialogs, menus, anything floating above the page) are exempt.
  const dropShadow = (shadow) =>
    shadow !== 'none' &&
    shadow
      .split(/,(?![^(]*\))/)
      .some((part) => {
        const [x = 0, y = 0, blur = 0] = part.replace(/rgba?\([^)]*\)/, '').trim().split(/\s+/).map((v) => parseFloat(v) || 0)
        return !part.includes('inset') && (x !== 0 || y !== 0 || blur !== 0)
      })
  const overlay = (e) => {
    const s = getComputedStyle(e)
    return (
      e.matches('dialog, [popover], [role="menu"], [role="listbox"], [role="tooltip"], [role="dialog"]') ||
      ((s.position === 'fixed' || s.position === 'absolute') && s.zIndex !== 'auto' && Number(s.zIndex) > 1)
    )
  }
  const elevationSoup = all
    .filter((e) => {
      const s = getComputedStyle(e)
      return s.borderTopWidth !== '0px' && dropShadow(s.boxShadow) && !overlay(e)
    })
    .map(label)

  // T18 Colour on chrome: distinct saturated hues used as fills (accent sprawl).
  const hues = new Set(
    all
      .map((e) => getComputedStyle(e).backgroundColor)
      .filter((c) => !transparent(c) && saturation(c) > 0.45)
      .map((c) => Math.round((hue(c) ?? 0) / 30) * 30),
  )

  // T19 Loud solid buttons, by colour; more than one per colour family competes.
  const solidButtons = all
    .filter((e) => e.matches('button, a.btn, .btn') && !transparent(getComputedStyle(e).backgroundColor) && saturation(getComputedStyle(e).backgroundColor) > 0.35)
    .map((e) => `${getComputedStyle(e).backgroundColor} ${label(e)}`)

  return {
    gradients,
    gradientText,
    glow,
    glass,
    caps,
    stripes,
    pills,
    pillButtons,
    endless,
    iconTiles,
    iconSoup,
    kpiRows,
    emoji,
    metaStrings,
    dashCells,
    tiny,
    oneRadius: { radius: topRadius, share: radii.length ? +(topCount / radii.length).toFixed(2) : 0, boxes: radii.length },
    elevationSoup,
    accentHues: [...hues],
    solidButtons,
    words: words.split(/\s+/).filter(Boolean).length,
  }
}
