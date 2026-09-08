# Scadix Icon Generator

A utility that converts any PNG/BMP/JPG image into a multi-size `.ico` file, or generates the built-in Scadix Designer icon programmatically.

## Usage

### Convert an existing image to ICO
```bash
dotnet run -- "path\to\input.png" "path\to\output.ico"
```

### Generate the built-in Scadix Designer icon
```bash
# Output to current directory (ScadixDesigner.ico + ScadixDesigner.png)
dotnet run

# Output to specific path
dotnet run -- "d:\MyPath\MyIcon.ico"
```

## Output
- **ICO** file with 6 sizes: 256 × 256, 128 × 128, 64 × 64, 48 × 48, 32 × 32, 16 × 16
- **PNG** file at 512 × 512 (for use in Avalonia assets)

## How it works
- Uses `System.Drawing` (Windows Forms GDI+) to render or resize images
- Saves each size as a PNG frame inside the ICO container
- Fully supports transparency (32-bit ARGB)
