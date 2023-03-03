using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;

static class BuildCommand {
#pragma warning disable IDE1006
    const string KEYSTORE_PASS = "KEYSTORE_PASS";
    const string KEY_ALIAS_PASS = "KEY_ALIAS_PASS";
    const string KEY_ALIAS_NAME = "KEY_ALIAS_NAME";
    const string KEYSTORE = "keystore.keystore";
    const string BUILD_OPTIONS_ENV_VAR = "BuildOptions";
    const string ANDROID_BUNDLE_VERSION_CODE = "VERSION_BUILD_VAR";
    const string ANDROID_APP_BUNDLE = "BUILD_APP_BUNDLE";
    const string SCRIPTING_BACKEND_ENV_VAR = "SCRIPTING_BACKEND";
    const string VERSION_NUMBER_VAR = "VERSION_NUMBER_VAR";
    const string VERSION_iOS = "VERSION_BUILD_VAR";
#pragma warning restore IDE1006

    static string GetArgument(string name) {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++) {
            if (args[i].Contains(name)) {
                return args[i + 1];
            }
        }
        return null;
    }

    static string[] GetEnabledScenes() {
        return (
            from scene in EditorBuildSettings.scenes
            where scene.enabled
            where !string.IsNullOrEmpty(scene.path)
            select scene.path
        ).ToArray();
    }

    static BuildTarget GetBuildTarget() {
        string buildTargetName = GetArgument("customBuildTarget");
        Console.WriteLine(":: Received customBuildTarget " + buildTargetName);

        if (buildTargetName.TryConvertToEnum(out BuildTarget target))
            return target;

        Console.WriteLine($":: {nameof(buildTargetName)} \"{buildTargetName}\" not defined on enum {nameof(BuildTarget)}, using {nameof(BuildTarget.NoTarget)} enum to build");

        return BuildTarget.NoTarget;
    }

    static string GetBuildPath() {
        string buildPath = GetArgument("customBuildPath");
        Console.WriteLine(":: Received customBuildPath " + buildPath);
        return !string.IsNullOrEmpty(buildPath) ? buildPath
                                                : throw new Exception("customBuildPath argument is missing");
    }

    static string GetBuildName() {
        string buildName = GetArgument("customBuildName");
        Console.WriteLine(":: Received customBuildName " + buildName);
        return !string.IsNullOrEmpty(buildName) ? buildName
                                                : throw new Exception("customBuildName argument is missing");
    }

    static string GetFixedBuildPath(BuildTarget buildTarget, string buildPath, string buildName) {
        if (buildTarget.ToString().ToLower().Contains("windows")) {
            buildName += ".exe";
        }
        else if (buildTarget == BuildTarget.Android) {
            buildName += EditorUserBuildSettings.buildAppBundle ? ".aab" : ".apk";
        }
        return buildPath + buildName;
    }

    static BuildOptions GetBuildOptions() {
        if (TryGetEnv(BUILD_OPTIONS_ENV_VAR, out string envVar)) {
            string[] allOptionVars = envVar.Split(',');
            BuildOptions allOptions = BuildOptions.None;
            string optionVar;
            int length = allOptionVars.Length;

            Console.WriteLine($":: Detecting {BUILD_OPTIONS_ENV_VAR} env var with {length} elements ({envVar})");

            for (int i = 0; i < length; i++) {
                optionVar = allOptionVars[i];

                if (optionVar.TryConvertToEnum(out BuildOptions option)) {
                    allOptions |= option;
                }
                else {
                    Console.WriteLine($":: Cannot convert {optionVar} to {nameof(BuildOptions)} enum, skipping it.");
                }
            }

            return allOptions;
        }

        return BuildOptions.None;
    }

    static bool TryConvertToEnum<TEnum>(this string strEnumValue, out TEnum value) {
        if (!Enum.IsDefined(typeof(TEnum), strEnumValue)) {
            value = default;
            return false;
        }

        value = (TEnum)Enum.Parse(typeof(TEnum), strEnumValue);
        return true;
    }

    static bool TryGetEnv(string key, out string value) {
        value = Environment.GetEnvironmentVariable(key);
        return !string.IsNullOrEmpty(value);
    }

    static void SetScriptingBackendFromEnv(BuildTarget platform) {
        BuildTargetGroup targetGroup = BuildPipeline.GetBuildTargetGroup(platform);

        if (TryGetEnv(SCRIPTING_BACKEND_ENV_VAR, out string scriptingBackend)) {
            if (scriptingBackend.TryConvertToEnum(out ScriptingImplementation backend)) {
                Console.WriteLine($":: Setting ScriptingBackend to {backend}");
                PlayerSettings.SetScriptingBackend(targetGroup, backend);
            }
            else {
                string possibleValues = string.Join(", ", Enum.GetValues(typeof(ScriptingImplementation)).Cast<ScriptingImplementation>());
                throw new Exception($"Could not find '{scriptingBackend}' in ScriptingImplementation enum. Possible values are: {possibleValues}");
            }
        }
        else {
            ScriptingImplementation defaultBackend = PlayerSettings.GetDefaultScriptingBackend(targetGroup);
            Console.WriteLine($":: Using project's configured ScriptingBackend (should be {defaultBackend} for targetGroup {targetGroup}");
        }
    }

    static void PerformBuild() {
        BuildTarget buildTarget = GetBuildTarget();

        Console.WriteLine(":: Performing build");
        if (TryGetEnv(VERSION_NUMBER_VAR, out string bundleVersionNumber)) {
            if (buildTarget == BuildTarget.iOS) {
                bundleVersionNumber = GetIosVersion();
            }
            Console.WriteLine($":: Setting bundleVersionNumber to '{bundleVersionNumber}' (Length: {bundleVersionNumber.Length})");
            PlayerSettings.bundleVersion = bundleVersionNumber;
        }

        if (buildTarget == BuildTarget.Android) {
            HandleAndroidAppBundle();
            HandleAndroidBundleVersionCode();
            HandleAndroidKeystore();
        }

        string buildPath = GetBuildPath();
        string buildName = GetBuildName();
        BuildOptions buildOptions = GetBuildOptions();
        string fixedBuildPath = GetFixedBuildPath(buildTarget, buildPath, buildName);

        SetScriptingBackendFromEnv(buildTarget);

        BuildReport buildReport = BuildPipeline.BuildPlayer(GetEnabledScenes(), fixedBuildPath, buildTarget, buildOptions);

        if (buildReport.summary.result != BuildResult.Succeeded)
            throw new Exception($"Build ended with {buildReport.summary.result} status");

        Console.WriteLine(":: Done with build");
    }

    static void HandleAndroidAppBundle() {
        if (TryGetEnv(ANDROID_APP_BUNDLE, out string value)) {
            if (bool.TryParse(value, out bool buildAppBundle)) {
                EditorUserBuildSettings.buildAppBundle = buildAppBundle;
                Console.WriteLine($":: {ANDROID_APP_BUNDLE} env var detected, set buildAppBundle to {value}.");
            }
            else {
                Console.WriteLine($":: {ANDROID_APP_BUNDLE} env var detected but the value \"{value}\" is not a boolean.");
            }
        }
    }

    static void HandleAndroidBundleVersionCode() {
        if (TryGetEnv(ANDROID_BUNDLE_VERSION_CODE, out string value)) {
            if (int.TryParse(value, out int version)) {
                PlayerSettings.Android.bundleVersionCode = version;
                Console.WriteLine($":: {ANDROID_BUNDLE_VERSION_CODE} env var detected, set the bundle version code to {value}.");
            }
            else
                Console.WriteLine($":: {ANDROID_BUNDLE_VERSION_CODE} env var detected but the version value \"{value}\" is not an integer.");
        }
    }

    static string GetIosVersion() {
        if (TryGetEnv(VERSION_iOS, out string value)) {
            if (int.TryParse(value, out int version)) {
                Console.WriteLine($":: {VERSION_iOS} env var detected, set the version to {value}.");
                return version.ToString();
            }
            else
                Console.WriteLine($":: {VERSION_iOS} env var detected but the version value \"{value}\" is not an integer.");
        }

        throw new ArgumentNullException(nameof(value), $":: Error finding {VERSION_iOS} env var");
    }

    static void HandleAndroidKeystore() {
        PlayerSettings.Android.useCustomKeystore = false;

        if (!File.Exists(KEYSTORE)) {
            Console.WriteLine($":: {KEYSTORE} not found, skipping setup, using Unity's default keystore");
            return;
        }

        PlayerSettings.Android.keystoreName = KEYSTORE;

        if (TryGetEnv(KEY_ALIAS_NAME, out string keyaliasName)) {
            PlayerSettings.Android.keyaliasName = keyaliasName;
            Console.WriteLine($":: using ${KEY_ALIAS_NAME} env var on PlayerSettings");
        }
        else {
            Console.WriteLine($":: ${KEY_ALIAS_NAME} env var not set, using Project's PlayerSettings");
        }

        if (!TryGetEnv(KEYSTORE_PASS, out string keystorePass)) {
            Console.WriteLine($":: ${KEYSTORE_PASS} env var not set, skipping setup, using Unity's default keystore");
            return;
        }

        if (!TryGetEnv(KEY_ALIAS_PASS, out string keystoreAliasPass)) {
            Console.WriteLine($":: ${KEY_ALIAS_PASS} env var not set, skipping setup, using Unity's default keystore");
            return;
        }

        PlayerSettings.Android.useCustomKeystore = true;
        PlayerSettings.Android.keystorePass = keystorePass;
        PlayerSettings.Android.keyaliasPass = keystoreAliasPass;
    }
}
