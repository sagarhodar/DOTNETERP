    public const string LogoPath     =
        Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory,
        "Resources", "Ojaswat.png");
    // public const string LogoPath     = @"   // absolute path to logo image chaned 
    public const int    LogoHeight   = 90;
    public const int    LogoWidth    = 100;
    public const string LogoPosition = "left";


Build failed with 1 error(s) in 3.4s
C:\SAGAR\CODING\DOTNET\ERP\8\Phase03\InvoiceApp_V33\InvoiceSettings.cs(24,9): error CS0103: The name 'Path' does not exist in the current context


The build failed. Fix the build errors and run again.
The build failed. Fix the build errors and run again.
PS C:\SAGAR\CODING\DOTNET\ERP\8\Phase03\InvoiceApp_V33>

There are two reasons why your code is failing. 

### 1. Missing Namespace (The immediate error)
The `Path` class lives in the `System.IO` namespace. You need to add a `using` statement at the very top of your file.

### 2. `const` vs `static readonly` (The next error you will hit)
In C#, a `const` must be a value that the compiler can determine at **compile-time** (like a raw string or a number). 
Because `Path.Combine` and `AppDomain.CurrentDomain.BaseDirectory` are only calculated when the program is **running**, you cannot use `const`. You must use `public static readonly` instead.

---

### Fixed Code

Apply these changes to your `InvoiceSettings.cs` file:

```csharp
using System;
using System.IO; // 1. Add this to fix the "Path does not exist" error

namespace YourNamespace // Keep your existing namespace
{
    public class InvoiceSettings
    {
        // 2. Change 'const' to 'static readonly'
        public static readonly string LogoPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "Resources", "Ojaswat.png");

        public const int LogoHeight = 90;
        public const int LogoWidth = 100;
        public const string LogoPosition = "left";
    }
}
```

### Summary of Changes:
1.  **`using System.IO;`**: Tells the compiler where to find the `Path` class.
2.  **`static readonly`**: Allows you to use functions like `Path.Combine` to set a value that won't change after the app starts, but is calculated at runtime.

Restore complete (0.6s)
  Ojaswat_frmnr2l1_wpftmp net8.0-windows win-x64 failed with 1 error(s) (3.1s)
    C:\SAGAR\CODING\DOTNET\ERP\8\Phase03\InvoiceApp_V33\InvoiceSettings.cs(25,9): error CS0133: The expression being assigned to 'InvoiceSettings.LogoPath' must be constant

Build failed with 1 error(s) in 4.5s
C:\SAGAR\CODING\DOTNET\ERP\8\Phase03\InvoiceApp_V33\InvoiceSettings.cs(25,9): error CS0133: The expression being assigned to 'InvoiceSettings.LogoPath' must be constant

The build failed. Fix the build errors and run again.
PS C:\SAGAR\CODING\DOTNET\ERP\8\Phase03\InvoiceApp_V33> 

The error **CS0133** confirms exactly what we suspected: **`const` cannot be used with functions like `Path.Combine`**. 

In C#, a `const` must be a hardcoded value (like `"C:\Images\Logo.png"`). Since your path is being "calculated" when the program starts, you must change the keyword to **`static readonly`**.

Update your code to look like this:

```csharp
using System;
using System.IO;

public class InvoiceSettings
{
    // Change 'const' to 'static readonly'
    public static readonly string LogoPath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory,
        "Resources", "Ojaswat.png");

    public const int LogoHeight = 90;
    public const int LogoWidth = 100;
    public const string LogoPosition = "left";
}
```

### Why this fixes it:
1.  **`const`**: The value is set at **Compile-Time**. The computer needs to know the exact string before the program even runs. It cannot run `Path.Combine` yet.
2.  **`static readonly`**: The value is set at **Runtime**. The computer waits until the program starts, runs `Path.Combine` to find the correct folder, and then locks the variable so it can't be changed again.

**Note:** If you use this variable in other classes, you can still access it the exact same way: `InvoiceSettings.LogoPath`.


To make this work so that you have a **default logo** upon installation but can **update it later via the UI**, you need to make three changes:

### 1. Change the Code (InvoiceSettings.cs)
You must remove `const` and `readonly`. Use a **static property**. This allows the program to start with the default path, but allows your UI code to overwrite that path when the user selects a new image.

```csharp
using System;
using System.IO;

public static class InvoiceSettings
{
    // 1. Use a static property so it can be updated by your UI
    public static string LogoPath { get; set; } = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, 
        "Resources", 
        "Ojaswat.png");

    public const int LogoHeight = 90;
    public const int LogoWidth = 100;
    public const string LogoPosition = "left";
}
```

### 2. Include the Default Logo in your Build
For the "Default" logo to exist when the user installs the software, you must ensure the file is copied to the folder during the build process.

1.  Create a folder in your project named `Resources`.
2.  Put your `Ojaswat.png` inside that folder.
3.  Right-click `Ojaswat.png` in Visual Studio and select **Properties**.
4.  Set **Copy to Output Directory** to **Copy if newer**.

*Alternatively, add this to your `.csproj` file:*
```xml
<ItemGroup>
  <None Update="Resources\Ojaswat.png">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
</ItemGroup>
```

### 3. How to update it from your UI
When the user selects a new logo in your settings screen, you simply update the variable:

```csharp
// Example code for your "Browse Logo" button
var openFileDialog = new Microsoft.Win32.OpenFileDialog();
if (openFileDialog.ShowDialog() == true)
{
    // Update the setting at runtime
    InvoiceSettings.LogoPath = openFileDialog.FileName;
    
    // TODO: Save openFileDialog.FileName to your Database or Config file 
    // so it remembers this path next time the app starts.
}
```

### Important Note on Persistence:
The change to `LogoPath` will only last as long as the app is open. If you want the app to **remember** the new logo after it is closed and reopened, you should:
1.  Save the new path to a `Settings` file or a Database.
2.  In your App startup code, load that saved path into `InvoiceSettings.LogoPath`.

  <ItemGroup>
    <Resource Include="Resources\app.ico" />
  <None Update="Resources\Ojaswat.png">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
  <None Update="Resources\Qr.png">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
  </ItemGroup>


using System;
using System.IO; 
namespace Ojaswat;

public static class InvoiceSettings

    public static string LogoPath { get; set; } = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, 
        "Resources", 
        "Ojaswat.png");
    // public const string LogoPath     = @"   // absolute path to logo image
    public const int    LogoHeight   = 90;
    public const int    LogoWidth    = 100;
    public const string LogoPosition = "left";
    // ── QR CODE ───────────────────────────────────────────────────────────────
    public const string QRCodePath = @"";     // absolute path to QR code image
    public const int    QRCodeSize = 90; suggest code here also

To keep it consistent with your Logo settings, you should use the same `static string` pattern for the QR code. Since you already added `Resources\Qr.png` to your `.csproj` file, we will point to that as the default.

Here is the corrected and completed **InvoiceSettings.cs**:

```csharp
using System;
using System.IO; 

namespace Ojaswat
{
    public static class InvoiceSettings
    {
        // ── LOGO ────────────────────────────────────────────────────────────────
        public static string LogoPath { get; set; } = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, 
            "Resources", 
            "Ojaswat.png");

        public const int LogoHeight = 90;
        public const int LogoWidth  = 100;
        public const string LogoPosition = "left";

        // ── QR CODE ──────────────────────────────────────────────────────────────
        // Defaulting to the Qr.png in your Resources folder
        public static string QRCodePath { get; set; } = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, 
            "Resources", 
            "Qr.png");

        public const int QRCodeSize = 90;
    }
}
```

### What this does for you:

1.  **Fixed the Syntax Error:** Added the missing curly braces `{ }` after `public static class InvoiceSettings`.
2.  **Default Installation Path:** When you install the software, it will look inside the installation folder's `Resources` sub-folder for `Ojaswat.png` and `Qr.png`.
3.  **UI Updates:** Since these are `public static string` (not `const`), your UI settings page can overwrite these paths if the user selects a custom image from their own "Pictures" or "Downloads" folder.
4.  **Deployment:** Because you added the `<None Update...>` section to your `.csproj` file, when you build/publish the app, the `Resources` folder will be created automatically, and your default images will be placed inside it.

### Suggestion for your UI code:
When you allow the user to change the QR code in your app settings, you would do this:

```csharp
// Inside your "Change QR Code" button click:
var dialog = new Microsoft.Win32.OpenFileDialog();
if (dialog.ShowDialog() == true)
{
    Ojaswat.InvoiceSettings.QRCodePath = dialog.FileName;
}
```

To change the path from the installation folder (**Local/Programs**) to the user data folder (**Roaming**), you should use `Environment.SpecialFolder.ApplicationData`.

In Windows, `AppData\Roaming` is the correct place to store user-specific settings and files that might be modified after installation.

### Updated InvoiceSettings.cs

This code will now point to `C:\Users\sagar\AppData\Roaming\Ojaswat\Resources\`.

```csharp
using System;
using System.IO; 

namespace Ojaswat
{
    public static class InvoiceSettings
    {
        // This calculates the path to C:\Users\YourName\AppData\Roaming\Ojaswat\Resources
        private static readonly string RoamingFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
            "Ojaswat", 
            "Resources");

        // ── LOGO ────────────────────────────────────────────────────────────────
        public static string LogoPath { get; set; } = Path.Combine(RoamingFolder, "Ojaswat.png");

        public const int LogoHeight = 90;
        public const int LogoWidth  = 100;
        public const string LogoPosition = "left";

        // ── QR CODE ──────────────────────────────────────────────────────────────
        public static string QRCodePath { get; set; } = Path.Combine(RoamingFolder, "Qr.png");

        public const int QRCodeSize = 90;

        /// <summary>
        /// Call this method when the App starts to ensure the folders exist
        /// and the default images are copied from the installation folder to Roaming.
        /// </summary>
        public static void InitializeStorage()
        {
            // 1. Create the Roaming folder if it doesn't exist
            if (!Directory.Exists(RoamingFolder))
            {
                Directory.CreateDirectory(RoamingFolder);
            }

            // 2. Copy default files from Installation folder if they aren't in Roaming yet
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            
            string defaultLogo = Path.Combine(baseDir, "Resources", "Ojaswat.png");
            string defaultQr = Path.Combine(baseDir, "Resources", "Qr.png");

            if (File.Exists(defaultLogo) && !File.Exists(LogoPath))
                File.Copy(defaultLogo, LogoPath);

            if (File.Exists(defaultQr) && !File.Exists(QRCodePath))
                File.Copy(defaultQr, QRCodePath);
        }
    }
}
```

### Why this is better:
1.  **Permission Safety:** Apps usually don't have permission to write files into `C:\Local\Programs` without Admin rights. `AppData\Roaming` is owned by the user, so your UI can save new logos there without errors.
2.  **Persistence:** If you update the software, files in `Roaming` are usually kept, whereas files in the installation folder might be overwritten or deleted.
3.  **The `InitializeStorage` Method:** I added a helper method. You should call `Ojaswat.InvoiceSettings.InitializeStorage();` inside your `App.xaml.cs` or `Main` method. 
    *   It checks if the `Roaming` folder exists (creates it if not).
    *   If it doesn't find the logo in Roaming, it copies the "Default" one you shipped with your installer.

### To use it:
In your **App.xaml.cs** (or wherever your app starts):
```csharp
protected override void OnStartup(StartupEventArgs e)
{
    base.OnStartup(e);
    // Ensure the folder exists and has the default images
    Ojaswat.InvoiceSettings.InitializeStorage();
}
```


To include a confirmation alert, we will use `System.Windows.MessageBox` (assuming this is a WPF or WinForms app). 

The logic will check if the files are missing in the **Roaming** folder. If they are, it will ask the user: *"Default data not found. Would you like to load the default logo and QR code?"*

### Updated InvoiceSettings.cs

```csharp
using System;
using System.IO; 
using System.Windows; // Required for MessageBox

namespace Ojaswat
{
    public static class InvoiceSettings
    {
        // Path to C:\Users\YourName\AppData\Roaming\Ojaswat\Resources
        private static readonly string RoamingFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
            "Ojaswat", 
            "Resources");

        // ── LOGO ────────────────────────────────────────────────────────────────
        public static string LogoPath { get; set; } = Path.Combine(RoamingFolder, "Ojaswat.png");
        public const int LogoHeight = 90;
        public const int LogoWidth  = 100;
        public const string LogoPosition = "left";

        // ── QR CODE ──────────────────────────────────────────────────────────────
        public static string QRCodePath { get; set; } = Path.Combine(RoamingFolder, "Qr.png");
        public const int QRCodeSize = 90;

        /// <summary>
        /// Checks if data exists in Roaming. If not, asks user to load defaults.
        /// </summary>
        public static void InitializeStorage()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string defaultLogoSource = Path.Combine(baseDir, "Resources", "Ojaswat.png");
            string defaultQrSource = Path.Combine(baseDir, "Resources", "Qr.png");

            // Check if the files are missing in the Roaming folder
            bool logoMissing = !File.Exists(LogoPath);
            bool qrMissing = !File.Exists(QRCodePath);

            if (logoMissing || qrMissing)
            {
                // Show Confirmation Alert
                MessageBoxResult result = MessageBox.Show(
                    "Default logo/QR data was not found in your application profile. \n\nWould you like to load the default images now?", 
                    "Setup Default Resources", 
                    MessageBoxButton.YesNo, 
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        // Create folder if it doesn't exist
                        if (!Directory.Exists(RoamingFolder))
                        {
                            Directory.CreateDirectory(RoamingFolder);
                        }

                        // Copy Logo
                        if (File.Exists(defaultLogoSource))
                            File.Copy(defaultLogoSource, LogoPath, true);

                        // Copy QR
                        if (File.Exists(defaultQrSource))
                            File.Copy(defaultQrSource, QRCodePath, true);

                        MessageBox.Show("Default data loaded successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error loading default data: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }
    }
}
```

### Important Setup:

1.  **Namespace conflict:** If you are using **Windows Forms** instead of **WPF**, change `MessageBoxResult` to `DialogResult` and `MessageBoxButton.YesNo` to `MessageBoxButtons.YesNo`.
2.  **Where to call this:** You should call this once when your application starts (e.g., in `App.xaml.cs` or `MainWindow.xaml.cs` constructor).

```csharp
// Example (App.xaml.cs)
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Ojaswat.InvoiceSettings.InitializeStorage();
    }
}
```

### What happens now:
1.  The app starts and looks in `AppData\Roaming\Ojaswat\Resources`.
2.  If the images are NOT there, a popup asks **"Would you like to load default images?"**
3.  If the user clicks **Yes**, it copies them from your installation folder to the Roaming folder.
4.  If the user clicks **No**, it does nothing (allowing them to keep the folder empty or handle it later).
5.  If the images **already exist**, the popup never appears, and the app starts normally.







