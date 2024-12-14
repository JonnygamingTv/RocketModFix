# Rocket.AutoInstaller

It's a Module auto-installer (loader/bootstrapper), which installs RocketModFix automatically from GitHub, you can still use a manual way of installation, however, this one is more modern, simple and faster.

## Plans

If you want to implement one of the plans or have better ideas or ideas to change, feel free to know us about that!

- [x] Auto-Install from GitHub Releases.
- [ ] Auto-Install Local Build (so, no need to manually install RocketModFix everytime you test/update it, 1 click to build, restart server, and you're testing!).
	- [x] Json (or other type of config) Config with Options:
  		- [x] `EnableCustomInstall`: false/true (default is false).
    	- [x] `CustomInstallPath`: path to a build of RocketModFix.
    - [ ] (Not 100% yet) We can also an option in config (`AutoInstallRocketFromExtras`) to make auto installation of Rocket (LDM) from Extras.
    - [ ] Once we do a caching make sure to ignore the own directory in Module code, cuz it will block the load process (see `BlockIfRocketInstalled`)!
- [ ] Caching.
	- [ ] Check if GitHub release is newer than current cached version and ONLY then install new version, also use Retry (5 seconds, 5 attempts or so) in case of GitHub's down or problems with internet.
	- [ ] For safe usage without Internet Connection.
- [x] ([Details](https://github.com/RocketModFix/RocketModFix/issues/119)) Block installation if Rocket already installed (`BlockIfRocketInstalled` in config).