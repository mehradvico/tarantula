# iOS design notes for the Tarantula console

Read from Apple's own pages on 2026-11-16 (the HIG renders with JavaScript, so it was read in a real browser). Paraphrased, not copied.
Sources: [Materials](https://developer.apple.com/design/human-interface-guidelines/materials) · [Sidebars](https://developer.apple.com/design/human-interface-guidelines/sidebars) · [Toolbars](https://developer.apple.com/design/human-interface-guidelines/toolbars) · [Typography](https://developer.apple.com/design/human-interface-guidelines/typography) · [Layout](https://developer.apple.com/design/human-interface-guidelines/layout) · [apple.com/ios](https://www.apple.com/ios/) · [Liquid Glass announcement](https://www.apple.com/newsroom/2025/06/apple-introduces-a-delightful-and-elegant-new-software-design/).

## What "latest iOS" means here: Liquid Glass

- Two layers. **Navigation and controls** (sidebar, toolbar/header, tab bar) are a floating Liquid Glass layer. **Content** sits below it and uses standard materials or plain surfaces. Do not put glass in the content layer; use it sparingly on custom controls.
- Glass lets content scroll and peek through but must stay legible. Use the **regular** variant when there is text (sidebars, popovers, alerts). The **clear** variant is only for glass over rich media, and needs a ~35% dark dimming layer over bright content.
- Content extends underneath the floating bars (sidebar, header). Separate bars from content with a scroll-edge effect, not with a solid or tinted background strip.
- Controls are rounded and **concentric** with the corners of the bar that holds them (inner radius = outer radius − padding).
- Apple's iOS page (2026) says the newest update improves contrast and uniform refraction, and adds a user slider from ultra-clear to fully tinted. So the glass must adapt to user/system settings: honour `prefers-reduced-transparency`, `prefers-contrast` and `prefers-reduced-motion`.
- Tab bars shrink on scroll to focus on content and expand when scrolling up.

## Sidebar (HIG)

- Floats above content in the glass layer; extend rich content beneath it.
- At most **two levels** of hierarchy; group with disclosure controls; short group titles.
- Let people hide/show it; do not hide it by default (discoverability). Avoid critical actions at the bottom.
- Icons use the app tint by default; use fixed colours sparingly and only with a purpose.
- On small screens prefer a tab bar first; a convertible sidebar covers less-used areas.

## Toolbar / header (HIG)

- Holds: title of the current view, navigation (back, search), actions. Do not crowd it; put the least important actions in a More menu.
- Title: a word or short phrase, under about 15 characters, never the app name.
- Prefer simple symbols without borders; reduce custom backgrounds and tinted controls; do not give toolbar labels a colour similar to the content behind.
- Standard buttons have corner radii concentric with the bar.

## Typography (HIG)

- iOS default text size 17, minimum 11. Avoid Ultralight/Thin/Light weights; use Regular, Medium, Semibold, Bold.
- Few typefaces; build hierarchy with weight, size and colour. Support larger text (rem-based sizes, layouts that stack and grow).
- Text styles used by the console (iOS Large defaults): Large Title 34/41 · Title 1 28/34 · Title 2 22/28 · Title 3 20/25 · Headline 17/22 semibold · Body 17/22 · Callout 16/21 · Subhead 15/20 · Footnote 13/18 · Caption 12/16 (the size table sits at the end of the HIG page; these are Apple's long-standing defaults).
- Console font: Yekan Bakh (proprietary, licensed), regular/bold/black only.

## Layout (HIG)

- Most important content near the top and on the reading-start side (right in RTL); align and indent to show hierarchy; group with space, shapes or separators; progressive disclosure.
- Differentiate controls from content with glass + scroll-edge effect instead of solid backgrounds under controls.
- Adapt to size classes, text size and RTL; respect safe areas. Minimum touch target 44 pt.

## How this maps to the Pastil panel

Same skeleton as the Pastil panel (right-hand sidebar that collapses to a 84 px rail, sticky 80 px header with title, version badge, notice/report/profile dropdowns, mobile menu + bottom nav), but black and orange, floating and glassy.
