``` text
//  ███╗   ███╗ █████╗  ██████╗██╗  ██╗██╗███╗   ██╗ █████╗
//  ████╗ ████║██╔══██╗██╔════╝██║  ██║██║████╗  ██║██╔══██╗
//  ██╔████╔██║███████║██║     ███████║██║██╔██╗ ██║███████║
//  ██║╚██╔╝██║██╔══██║██║     ██╔══██║██║██║╚██╗██║██╔══██║
//  ██║ ╚═╝ ██║██║  ██║╚██████╗██║  ██║██║██║ ╚████║██║  ██║
//  ╚═╝     ╚═╝╚═╝  ╚═╝ ╚═════╝╚═╝  ╚═╝╚═╝╚═╝  ╚═══╝╚═╝  ╚═╝
//
//  ██████╗ ██████╗ ██╗██████╗  ██████╗ ███████╗
//  ██╔══██╗██╔══██╗██║██╔══██╗██╔════╝ ██╔════╝
//  ██████╔╝██████╔╝██║██║  ██║██║  ███╗█████╗
//  ██╔══██╗██╔══██╗██║██║  ██║██║   ██║██╔══╝
//  ██████╔╝██║  ██║██║██████╔╝╚██████╔╝███████╗
//  ╚═════╝ ╚═╝  ╚═╝╚═╝╚═════╝  ╚═════╝ ╚══════╝
//
//   ██████╗██╗  ██╗ █████╗ ███╗   ██╗ ██████╗ ███████╗██╗      ██████╗  ██████╗
//  ██╔════╝██║  ██║██╔══██╗████╗  ██║██╔════╝ ██╔════╝██║     ██╔═══██╗██╔════╝
//  ██║     ███████║███████║██╔██╗ ██║██║  ███╗█████╗  ██║     ██║   ██║██║  ███╗
//  ██║     ██╔══██║██╔══██║██║╚██╗██║██║   ██║██╔══╝  ██║     ██║   ██║██║   ██║
//  ╚██████╗██║  ██║██║  ██║██║ ╚████║╚██████╔╝███████╗███████╗╚██████╔╝╚██████╔╝
//   ╚═════╝╚═╝  ╚═╝╚═╝  ╚═╝╚═╝  ╚═══╝ ╚═════╝ ╚══════╝╚══════╝ ╚═════╝  ╚═════╝
```
# TODO
- [ ] Save last connection configuration to a JSON file, and preload it next time the user opens the app.
- [ ] Save also something like the last 10 command blocks?

# v0.8.8b
- Added `InvariantCulture` parsing for `Double` inputs: https://github.com/RobotExMachina/Machina-Bridge/issues/3

# v0.8.8
- Core update

# v0.8.6
- Add states to the UI.
- Add "Send on Enter" checkbox option to override immediate send behavior.
- Layout is now fully responsive and scalable.
- `Verbose` is now the default loglevel (to see relative actions values)
- Core update: https://github.com/RobotExMachina/Machina.NET/releases/tag/v0.8.6

---
# v0.8.5
- Core update: https://github.com/RobotExMachina/Machina.NET/releases/tag/v0.8.5

---
# v0.8.4
- Bug fixes with tool definitions
- Numbers serialize as `InvariantCulture`

---
# v0.8.3
- Add "Download drivers" button.
- Add version dump to console.
- WebSocket server URL is now customizable.
- Add client dis/connection notifications.
- Console input box now highlights selection.
- Add multiline input support pressing Ctrl+Enter
- Add parsing of multiple instructions
- Fix app crash whenever the WS server address was in use.
- Add `ArmAngle`, `CustomCode` and `ExernalAxis` with extaxtarget argument
- DEBUG mode now opens the app in 5 - DEBUG
- `MotionUpdates` are logged on the console
- Improved instruction parser.

---
# v0.8.2
- Tons of changes and improvements: better UI, logging system, queue monitor, smarter console, etc...

---
# v0.7.0
- Serialization of Machina Events into JSON objects is now handled by core.
- Update core and interface with GH.


---
# v0.6.4
- [x] Start a `CHANGELOG`
- [x] Streamline response messages
- [x] Add `ExternalAxis` Action
- [x] Add `CustomCode` Action


