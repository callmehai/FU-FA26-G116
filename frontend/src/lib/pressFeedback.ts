/**
 * A press animation for every button, driven from script rather than `:active`.
 *
 * `:active` lasts only while the pointer is held. A trackpad tap-to-click releases within a few
 * milliseconds, so a CSS transition keyed on `:active` never gets to run and the click looks like
 * it did nothing — only a hard physical press showed it. A keyframe animation started on press
 * always plays to the end, however short the press was.
 */
const PRESSABLE =
  '.btn, .icon-button, .select-trigger, .menu-item, .topbar-icon'
const PRESSED_CLASS = 'is-pressed'

function press(target: EventTarget | null) {
  const element = target instanceof Element ? target.closest<HTMLElement>(PRESSABLE) : null
  if (!element || element.matches(':disabled')) return

  // Restart rather than ignore when pressed again mid-animation (a quick double click).
  element.classList.remove(PRESSED_CLASS)
  void element.offsetWidth
  element.classList.add(PRESSED_CLASS)
  element.addEventListener('animationend', () => element.classList.remove(PRESSED_CLASS), { once: true })
}

export function installPressFeedback() {
  document.addEventListener('pointerdown', (event) => {
    if (event.button === 0) press(event.target)
  })
  // Keyboard activation of a focused control.
  document.addEventListener('keydown', (event) => {
    if ((event.key === 'Enter' || event.key === ' ') && !event.repeat) press(event.target)
  })
}
