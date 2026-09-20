<p align="center">
    <img src="Images/Goosetrap.png" width="220" alt="Goosetrap Logo">
</p>

<h1 align="center">Goosetrap</h1>

<p align="center">
    <strong>A fast, optimized, and customizable launcher for Roblox.</strong>
</p>

---

**Goosetrap** is a fork of Bloxstrap / Fishstrap designed specifically to maximize FPS, achieve extreme graphics optimization, and easily manage Roblox engine settings.

---

## 🚀 Key Features

- **Extreme FPS Unlocker** (Writes Roblox's own frame rate cap, up to 240 FPS - the client does not accept higher limits).
- **"Potato PC" Mode** (Lowest texture quality, grass and mesh detail removal, disabled shadows and post-processing effects - built on Fast Flags that are on Roblox's Fast Flag Allowlist).
- **Multi-Account Manager** (Quickly save multiple accounts and launch them directly without using a browser, enabling multi-instance/multi-roblox).
- **Active Clients Monitor** (View running Roblox clients, monitor RAM usage in real-time, and easily restart or close accounts).
- **Clean UI & Modern Design** (Sleek dark theme with simple navigation, fully translated into English and Russian).

---

## 📖 FPS Boost & Optimization Guide

To get the **maximum FPS** and remove stuttering in Roblox, follow these steps:

### 1. Unlocking FPS (FPS Cap)
1. Open **Goosetrap Settings**.
2. Navigate to the **"Engine Settings"** tab.
3. Scroll down to the **"Goosetrap Optimization"** section.
4. Set the **"FPS Cap"** value to `240` to lift the default Roblox 60 FPS lock.

> **Note:** The FPS cap is written into Roblox's own `FramerateCap` setting, which keeps working even though Roblox no longer applies Fast Flags for the frame rate. Roblox itself caps the value at `240`, so setting anything higher has no additional effect.

### 2. "Potato PC" Mode (Removing Textures and Graphics)
If you have a low-end computer or need maximum performance in demanding games (like simulators, shooters, etc.):
1. In the **"Goosetrap Optimization"** section, enable the **"Remove Textures and Graphics (Potato PC)"** toggle.
2. **What this does:**
   - Forces the lowest texture quality level, which massively reduces video memory and GPU load.
   - Cuts grass and mesh detail distances down to zero.
   - Disables shadows, Anti-Aliasing (MSAA) and post-processing effects (blur, bloom, color correction).
   - Uses the flags from the Roblox Fast Flag Allowlist wherever possible, see the section below.

### 3. Additional Settings for Performance
In the same **"Engine Settings"** tab, we recommend setting:
- **Anti-Aliasing (MSAA):** Set to `Disabled` (Potato PC mode turns this off automatically).
- **Rendering Mode:** Choose `Direct3D 11` (most stable API for Windows) or `Vulkan` (recommended for AMD graphics cards).
- **Texture Quality:** If Potato PC mode is disabled, select `Level 0 (Lowest)` for optimized texture resolution.

---

## 🧩 Roblox Fast Flag Allowlist

On **September 29, 2025** Roblox introduced the [Fast Flag Allowlist](https://devforum.roblox.com/t/allowlist-for-local-client-configuration-via-fast-flags/3966569):
the client now applies **only the Fast Flags that are on that list**. Every other flag written to
`ClientAppSettings.json` is silently ignored and has no effect at all.

What this means in Goosetrap:
- The **Fast Flag editor** shows the status of every flag - look at the **"Applied by Roblox"** column.
  A flag that is not allowlisted is marked as ignored and does nothing on the current Roblox version.
- The quick presets (**Potato PC**, **Ultra Graphics**, **Balanced**) set the allowlisted Fast Flags
  that match their goal, so they keep doing what their name says.
- Flags that fell off the allowlist are still written by the presets on purpose - as soon as Roblox
  allowlists them again they start working immediately, without needing a Goosetrap update.

---

## 📦 Download & Installation

1. Go to the [Releases](https://github.com/happy-g0ose/Goosetrap/releases) section of our repository.
2. Download the latest **`Goosetrap.exe`** executable.
3. Run the installer to configure your preferences and set up your Goosetrap app directory.
4. To open the settings menu later, launch Goosetrap with the `-menu` argument or open it from the Start Menu.

---

*Goosetrap is an open-source, non-profit community project. Roblox is a registered trademark of Roblox Corporation.*
