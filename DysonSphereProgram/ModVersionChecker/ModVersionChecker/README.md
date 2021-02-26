# ModVersionChecker

Forgot the version of installed mod?

Not knowing mod got updated?

An in-game mod version checker might just right for you.

The mod display all installed BepInEx plugins' version and tries to fetch newer version from ThunderStore.

Default hotkey for toggling mod's window is `LeftCtrl + F5`

![ScreenShot](https://raw.githubusercontent.com/yyuueexxiinngg/BepInEx-Plugins/master/DysonSphereProgram/ModVersionChecker/ModVersionChecker/Assets/Screenshot_1.1_EN.png)

### Installation

- Put `ModVersionChecker.dll` into the BepInEx `plugins` folder,
  e.g. `C:\Program Files x86 \Steam\steamapps\common\Dyson Sphere Program\BepInEx\plugins\ModVersionChecker.dll`

### ModDataList

ModDataList holds references mapping from mods' `full name` on ThunderStore to actual `GUID` in BepInEx. Checker would
not understand which package on ThunderStore is related to installed plugin if it's not listed thus not able to check
newer version for it.

#### ModDataList used in checker can be found here: [Github/.../ModDataList.xml](https://github.com/yyuueexxiinngg/BepInEx-Plugins/blob/master/DysonSphereProgram/ModVersionChecker/ModVersionChecker/Assets/ModDataList.xml) Checker will try to fetch this list at game launch

### Add mod info to ModDataList

#### Recommended:

- Create Pull Request on
  file [ModDataList.xml](https://github.com/yyuueexxiinngg/BepInEx-Plugins/blob/master/DysonSphereProgram/ModVersionChecker/ModVersionChecker/Assets/ModDataList.xml)
  to repository [https://github.com/yyuueexxiinngg/BepInEx-Plugins](https://github.com/yyuueexxiinngg/BepInEx-Plugins)
- Or file an issue request for adding new mod info
- Once merged, checker will fetch it on the next launch

#### Locally:

- Add mod info into local `XML` file located at `DSPGameRoot\BepInEx\data\ModVersionChecker\PreservedModData.xml`
- Entries in this file will overwrite entries fetched online

### FAQ

- **Latest version showing `Unsupported`**
    - Checker is fetching mod data list, or the mod is not found in mod data list, all mods listed in here are
      supported: [Github/.../ModDataList.xml](https://github.com/yyuueexxiinngg/BepInEx-Plugins/blob/master/DysonSphereProgram/ModVersionChecker/ModVersionChecker/Assets/ModDataList.xml)
- The current version is not correct, I've made sure the installed mod is the latest
    - Mod developer did not change the version fed into BepInEx in code upon releasing
    - Temporarily solution is to click the `Set As Lastest` button to set a custom version for that mod same as the
      latest version. Checker will assume custom version as installed version later on. Custom versions are saved
      at `DSPGameRoot\BepInEx\data\ModVersionChecker\CustomModCurrentVersionList.xml`
- Mods like [QTool_Model_plus](https://dsp.thunderstore.io/package/sherlockHlb/QTool_Model_plus/) showing wrong latest
  version
    - Mod developer did not change the `GUID` of his modified mod, checker use `GUID` to identify mods. In this case, it
      points to the original mod
- Once `Set As Latest` was used, updated plugin not getting correct current version
    - Resolved in v1.2
- Mod was supported, but now showing `Unsupported` after update
    - Mod developer changed mod's `GUID`, please file an issue

### Configuration

#### Method A

- Download and install `BepInEx.ConfigurationManager`
- Hit Configuration Manager's default hotkey `F1` to open management window
- Find ModVersionChecker Mod and change preferred fields.

#### Method B

- Locate `com.github.yyuueexxiinngg.plugin.modversionchecker.cfg`
- Open with a text editor
- Find and change preferred fields.

### Change log

#### V1.2

- **Fix once `Set As Latest` was used, checker will always use that version as current version even if the plugin
  actually got updated and developer set the correct version.**
- Now checker window will be closed automatically when game play starts

#### V1.1

- Add support for setting mod current version as latest in case mod developer forgot to change the version fed into
  BepInEx in code upon releasing a new version

### Any feedback? Post in [GitHub repo](https://github.com/yyuueexxiinngg/BepInEx-Plugins/)

# Mod版本检测器

忘记安装的Mod是什么版本的?

不知道Mod有更新了?

本Mod会显示所有安装的插件Mod版本并尝试在ThunderStore上获取新版本号

默认`左 Ctrl + F5`开关Mod窗口

![ScreenShot](https://raw.githubusercontent.com/yyuueexxiinngg/BepInEx-Plugins/master/DysonSphereProgram/ModVersionChecker/ModVersionChecker/Assets/Screenshot_1.1_CN.png)

### 安装方法

- 将 `ModVersionChecker.dll` 放入BepInEx的 `plugins`文件夹,
  如: `C:\Program Files x86 \Steam\steamapps\common\Dyson Sphere Program\BepInEx\plugins\ModVersionChecker.dll`

### ModDataList

Mod列表保存BepInEx中插件的`GUID`与其在ThunderStore上`Full name`的映射关系, 如果Mod不在列表中检测器就不知道哪个版本号与安装的插件对应导致无法检测其版本更新

#### 检测器用到的ModDataList在: [Github/.../ModDataList.xml](https://github.com/yyuueexxiinngg/BepInEx-Plugins/blob/master/DysonSphereProgram/ModVersionChecker/ModVersionChecker/Assets/ModDataList.xml) 每次启动游戏时检测器时都会尝试从这里获取最新的列表

### 如何向列表中添加Mod项目

#### 推荐做法:

- 在本插件的[GitHub项目](https://github.com/yyuueexxiinngg/BepInEx-Plugins)
  中对[ModDataList.xml](https://github.com/yyuueexxiinngg/BepInEx-Plugins/blob/master/DysonSphereProgram/ModVersionChecker/ModVersionChecker/Assets/ModDataList.xml)
  新建PR
- 或者在GitHub中新建Issue请求添加
- 如果PR被合并, 将会在检测器下一次启动获取新列表时生效

#### 本地添加:

- 将Mod信息添加至本地存储的`XML`文件, 路径为`游戏根目录\BepInEx\data\ModVersionChecker\PreservedModData.xml`
- 这里保存的列表项目会覆盖从网上获取的项目

### 常见问题

- **最新版本中显示 `未支持`**
    - 检测器正在获取Mod列表中, 或此Mod并未在列表中列出,
      所有支持检测更新的Mod在这里列出: [Github/.../ModDataList.xml](https://github.com/yyuueexxiinngg/BepInEx-Plugins/blob/master/DysonSphereProgram/ModVersionChecker/ModVersionChecker/Assets/ModDataList.xml)
- 目前版本不正确, 已经确保安装的Mod为最新版本
    - Mod开发者发布时没有修改代码中传递给BepInEx的版本号
    - 临时解决方法为点击`设置为最新版本`按钮对Mod将最新版本设置为自定义版本, 之后检测器会将自定义版本视为已安装的版本,
      自定义版本保存在: `游戏根目录\BepInEx\data\ModVersionChecker\CustomModCurrentVersionList.xml`
- 如[QTool_Model_plus](https://dsp.thunderstore.io/package/sherlockHlb/QTool_Model_plus/) 这样的Mod最新版本不正确
    - Mod开发者修改Mod时没有修改其`GUID`, 检测器依赖`GUID`来区分Mod, 在这个例子中, 他指向了原Mod
- 一旦使用过 `设置为最新版本`, 更新后的插件获取的目前版本不正确
    - v1.2 中已修复
- Mod本来支持检测版本, 更新后变成`未支持`
    - Mod开发者更改了插件的`GUID`, 请在GitHub中新建Issue

### 配置方法

#### 方法A

- 下载安装`BepInEx.ConfigurationManager`
- 游戏中按`ConfigurationManager`默认键`F1`开启MOD配置管理窗口进行配置

#### 方法B:

- 用文本编辑器打开 `游戏目录/BepInEx/config/com.github.yyuueexxiinngg.plugin.modversionchecker.cfg`
- 修改想要的设置, 将在下一次启动时生效

### 更新日志

#### V1.2

- **修复一旦使用过 `设置为最新版本`, 检测器会一直把此版本号当做目前版本, 就算插件更新过且开发者已经把版本设置正确**
- 现在检测器窗口会在开始游玩时自动关闭

#### V1.1

- 添加支持将Mod最新版本设置为已安装的版本, 避免当Mod开发者发布新版本后忘记修改代码中的版本时总是提示此Mod有更新

### 有任何反馈? [GitHub repo](https://github.com/yyuueexxiinngg/BepInEx-Plugins/)