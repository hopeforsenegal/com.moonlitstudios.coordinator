# Coordinator
[![License](https://img.shields.io/badge/license-MIT-green)](https://github.com/hopeforsenegal/immediatestyle/blob/master/LICENSE.md)

**Coordinator** A dead easy way to coordinate multiple instances of Unity. With it, you will be able to boot up multiple Unity Editors in order for them to help iterate with your **multiplayer** game. You can choose to have stand alone Editors, Editors that go into playmode, or Editors that play until a test is finished. 

<img width="1120" alt="Screenshot 2024-11-08 at 4 49 56 PM" src="https://github.com/user-attachments/assets/0bbe15bb-643b-472d-8432-5c92d1766138">

## Features

* **Standalone** interact with your [symlinked](https://en.wikipedia.org/wiki/Symbolic_link) or hard copy editors manually as if you created them and opened them yourself. Symlinked Editors have the benefit of reflecting script changes across the Main Editor and other symlinked projects.
* **Playmode** your additional editors will go into playmode when the original main editor goes into playmode. Perfect for getting into a multiplayer game with fewer steps. 
* **TestAndPlaymode** is the solution to be even less hands on. The editors will go into playmode until the main editor calls ```Editors.TestComplete``` and then call a post test method for you (perfect for going through a set of tests and then uploading a build).
* **Scripting Defines** so you can have one Editor run as **Server** and the other as **Client** (or perhaps **Demo**).

## Installation

- Add this GitHub URL to your package manager or, instead, in your 'manifest.json' add
```json
  "dependencies": {
	...

    	"com.moonlitstudios.coordinator": "https://github.com/hopeforsenegal/com.moonlitstudios.coordinator.git",

	...
  }
```

None of that working? **Honestly, just reach out us!** (Links & methods towards the bottom).


## Examples
https://github.com/hopeforsenegal/com.moonlitstudios.coordinator/blob/38f6aa3adc2beb64eb2c19e765a502b73efb6788/Test~/Test.cs#L8-L20

https://github.com/hopeforsenegal/com.moonlitstudios.coordinator/blob/38f6aa3adc2beb64eb2c19e765a502b73efb6788/Test~/Test.cs#L35-L39

## How does it work?
Additional Editors are created with [Symlinks](https://en.wikipedia.org/wiki/Symbolic_link) or Hard copies. We use files on disk that allow each editor to communicate with each other (easier then rolling a socket solution or more elaborate interprocess communication... also benefits from surviving Domain Reloads). That's 90% of how it works.

## Need Help or want to chat?
Feel free to just drop us a line on [Discord](https://discord.gg/8y87EEaftE). It's always better to have a real conversation and we can also screen share there. It's also not hard to reach us through our various other socials. There we can talk about the individual needs that you might have with your multiplayer projects.

</br>

___
## Other Unity Packages
Check out [Immediate Style](https://github.com/hopeforsenegal/com.moonlitstudios.immediatestyle) & [Asset Stripper](https://github.com/hopeforsenegal/com.moonlitstudios.assetstripper)

## Support this project 
Please, please, please!! ⭐ Star this project! If you truly feel empowered at all by this project please give [our games](https://linktr.ee/moonlit_games) a shot (and drop 5 star reviews there too!). Each of these games are supported by this tool 

<img width="256" alt="Screenshot 2024-11-04 at 10 43 52 AM" src="https://github.com/user-attachments/assets/85141dc9-110e-4a8d-b684-6c9a686c278b">

[Apple](https://apps.apple.com/us/app/caribbean-dominoes/id1588590418)
[Android](https://play.google.com/store/apps/details?id=com.MoonlitStudios.CaribbeanDominoes)

<img width="256" alt="Screenshot 2024-11-04 at 10 43 52 AM" src="https://github.com/user-attachments/assets/4266f475-ac9b-4176-9f97-985b8e1025ce">

[Apple](https://apps.apple.com/us/app/solitaire-islands/id6478837950)
[Android](https://play.google.com/store/apps/details?id=com.MoonlitStudios.SolitaireIslands)

<img width="256" alt="Screenshot 2024-11-04 at 10 43 52 AM" src="https://github.com/user-attachments/assets/13ba91c7-53b4-4469-bdd0-9f0598048a28">

[Apple](https://apps.apple.com/us/app/ludi-classic/id1536964897)
[Android](https://play.google.com/store/apps/details?id=com.MoonlitStudios.Ludi)


Last but not least, drop some follows on the following socials if you want to keep updated on the latest happenings 😊

https://www.twitch.tv/caribbeandominoes

https://www.facebook.com/CaribbeanDominoes

https://x.com/moonlit_studios

https://x.com/_quietwarrior
