# Steam Release Notes — policies, store page, controllers

Checked against Steamworks and Unity docs on 2026-09-29. Steam changes these
rules, so re-check the linked pages before paying the fee or submitting
anything. Anything marked **(unverified)** couldn't be confirmed from an
official page.

No Steamworks SDK is in the project yet, so every "game side" point below is
about Unity itself.

---

## 1. Release checklist and timeline

| Step | Rule | Source |
|---|---|---|
| Steam Direct fee | **$100 per app**. Recouped once the app earns **$1,000 Adjusted Gross Revenue**. | [App fee](https://partner.steamgames.com/doc/gettingstarted/appfee) |
| Waiting period | **30 days** between paying the fee and releasing. Valve uses it to verify identity and tax details. The date runs from the fee, so pay early. | [Steam Direct](https://partner.steamgames.com/steamdirect) (confirmed by several publishing guides) |
| Store page review | Usually **3–5 business days**. Valve says to submit **at least 7 days** before you want the page live. | [Releasing](https://partner.steamgames.com/doc/store/releasing) |
| Coming Soon | The page must be public as Coming Soon for **at least 2 weeks** before release. | same |
| Checklists | Both the **Store Presence** and **Game Build** checklists must be completed, reviewed and approved. Builds are reviewed separately from the store page. | same |
| Release | Manual: press **Release App**. Needs the "Publish app changes" and "Manage pricing and discounts" permissions on the account. | same |

**Plan backwards from the launch date L:** pay the fee before L−30,
submit the store page before L−21 (7 days for review plus 14 days Coming
Soon), and submit the build with at least a week to spare.

**Demo:** a demo is a separate app attached to the main game. It needs its own
store and build review but no second fee. Next Fest entry needs a demo and a
Coming Soon page; check the next Fest's sign-up deadline on the Steamworks
events page.

## 2. Content survey (required before review)

From [Content survey](https://partner.steamgames.com/doc/gettingstarted/contentsurvey):
- **General content**: answers become regional age ratings on the store page.
  You can also submit official ratings you already have.
- **Mature content**: violence, gore and so on. *"You must disclose all the
  adult content you've uploaded in your builds, even if it's not accessible."*
  Blood splatter and gore (`ChunkedGorePainter`, `BloodSplatterEffect`) count,
  so declare them.
- **AI-generated content disclosure**:
  - *Pre-generated* (made with AI during development and shipped in the game):
    art, audio, text or code assets. Disclose them; they're judged by the same
    rules as any other content.
  - *Live-generated* (made by AI while the game runs): you also have to explain
    your guardrails. This game has none, so it's N/A unless that changes.
  - Before submitting, go through the asset folders (`2d Assets`, `Audio`,
    mascot and shop art, store capsules) and note anything made with AI tools.

## 3. Store page assets (required)

From [Standard assets](https://partner.steamgames.com/doc/store/assets/standard):

| Asset | Size (px) | Required |
|---|---|---|
| Header capsule | 920 × 430 | yes |
| Small capsule | 462 × 174 | yes |
| Main capsule | 1232 × 706 | yes |
| Vertical capsule | 748 × 896 | yes |
| Screenshots | ≥ 1920 × 1080, 16:9, **at least 5** | yes |
| Page background | 1438 × 810 | no |

- Screenshots must be **gameplay only**: no concept art, cinematics or
  marketing text. At least 4 should be marked **suitable for all ages**, or the
  page gets hidden in some parts of Steam. With gore in the game, pick those 4
  deliberately.
- Every capsule needs the logo, clearly legible.
- The library assets (library capsule, hero, logo) are on a separate page;
  fill them in from the Steamworks checklist.

## 4. Controller support — what to declare

### What the game supports today (from the code)
- Input goes entirely through **Unity Input System 1.14.2**. Gamepad bindings
  use the generic `<Gamepad>` layout, so any pad Unity recognises works with
  the same bindings.
- Menus, shop, slot select and the new virtual keyboard all work with a pad,
  and rebinding is available (Controls tab).
- `BindingLabels` shows **PlayStation names** (Cross, Square...) when
  `Gamepad.current` is a DualShock or DualSense, and Xbox names otherwise.
- No rumble, lightbar or gyro is used.

### Unity's supported devices (Windows)
From [Input System 1.14 supported devices](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.14/manual/SupportedDevices.html):

| Controller | Windows | Notes |
|---|---|---|
| Xbox 360 / Xbox One / Series (XInput) | ✅ | the most reliable path |
| DualShock 4 (PS4) | ✅ | the DS4 USB wireless adapter isn't supported |
| DualSense (PS5) | ✅ | "via USB HID"; rumble and lightbar don't work over Bluetooth (not used here) |
| Switch Pro | ✅ per the table | Joy-Cons **not** supported on Windows/Mac. Needs a hardware test. |
| Generic HID pads | ✅ | layout may be wrong without a Steam Input remap |

Linux: DualSense shows **"No"**. The Steam Deck uses the Windows build through
Proton, and its built-in controls reach the game as an Xbox pad through Steam
Input, so this doesn't affect the Deck.

### Bluetooth: will it work?
**Yes for Xbox pads. Very likely for PS4/PS5 pads, but test it:**
- **Xbox pads over Bluetooth** are handled by Windows and reach Unity as
  XInput, the same as USB.
- **DualShock 4 / DualSense over Bluetooth**: Unity reads them as HID devices,
  and its docs only rule out **rumble and lightbar** over Bluetooth (the game
  uses neither). They don't say outright that buttons and sticks work over
  Bluetooth **(unverified)**. Test each pad over Bluetooth before ticking the
  PlayStation boxes.
- **When launched from Steam, this mostly doesn't matter.** By default Steam
  Input reads the physical pad itself, over USB or Bluetooth, and *"inject[s]
  an emulated Xbox controller device"*
  ([Steam Input emulation](https://partner.steamgames.com/doc/features/steam_controller/steam_input_gamepad_emulation_bestpractices)).
  Unity then sees an **Xbox pad**, whatever the player holds.

### The Steam Input catch: button labels
When Steam Input emulates, `Gamepad.current` is an XInput pad, so
`BindingLabels` shows **Xbox names to a PS5 player**. Two ways to handle it:
1. **Opt PlayStation pads out of Steam Input** for this app (Steamworks →
   Application → Steam Input settings). Unity then reads the DS4/DualSense
   natively and the PS names already work. The Bluetooth test above becomes
   mandatory.
2. **Keep Steam Input** and later integrate Steamworks.NET, asking Steam for
   the real pad type (`GetControllerForGamepadIndex` → `GetInputTypeForHandle`)
   to pick labels. Steam's own advice for games with Xbox support is to tick
   every Steam Input device checkbox *except* Xbox.

Option 1 costs no code and fits the game as it is now. Choose it unless
Steamworks gets integrated for other reasons (achievements, cloud saves).

### Store page declaration
Since Nov 2023 Steamworks has a controller questionnaire
([announcement](https://steamcommunity.com/groups/steamworks/announcements/detail/3684558162504860651),
[overview](https://www.gamedeveloper.com/business/steam-will-let-devs-put-playstation-controller-support-on-store-pages)).
It asks separately about **Xbox**, **DualShock (PS4)** and **DualSense (PS5)**,
and the store page shows Full or Partial support for each. PS support is not
assumed from Xbox support. The exact questions are **(unverified)**, since the
announcement page didn't load here; they cover whether every feature is
playable with the pad and whether on-screen prompts match the pad.

What to answer, as things stand:
- **Xbox: Full.** Everything in the game works with a pad, including naming a
  save slot with the virtual keyboard.
- **DualShock / DualSense: Full**, but only after the opt-out in option 1 and
  a Bluetooth and USB test showing PS names on every prompt. Until then,
  declare only Xbox.
- **Switch Pro / generic**: no store checkbox. Steam Input covers them, and
  they appear as an Xbox pad.

## 5. Before submitting

- [ ] Pay the fee (starts the 30-day clock)
- [ ] Content survey, including gore and the AI disclosure
- [ ] Capsules, 5+ gameplay screenshots (4 all-ages), trailer
- [ ] Store page submitted, then Coming Soon for 14+ days
- [ ] Steam Input settings: PlayStation opt-out, or the Steamworks.NET route
- [ ] Pad tests: Xbox over USB and Bluetooth, DS4 over USB and Bluetooth,
      DualSense over USB and Bluetooth, launched from Steam **and** directly
- [ ] Controller questionnaire answered to match those tests
- [ ] Build review; test the Steam Deck (Proton) if you want the Deck badge
- [ ] Release App
