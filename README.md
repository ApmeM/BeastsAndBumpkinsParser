B&B box files converter
==========
Console utility to convert original B&B resource .BOX files into unpacked readable formats:

- BOX files converted to folder structures with unpacked content
- TXT files unpacked as is with the text inside
- MFB files are spriteshees, converted to .PNG
- MIS files converted to [tiled](https://www.mapeditor.org/) .TMX
- M10 files are sound files, converted to wav
- FDF - NOT UNPACKED font files.

Usage
==========

1. Download original game iso file (for example from https://www.myabandonware.com/game/beasts-bumpkins-bh1#download)
2. Unpack it (for example to ~/Downloads/BB/)
3. Clone this repository
4. Change directory to src
5. Run the dotnet command `dotnet run ~/Downloads/BB/res/ ../Result`

The result:

In ../Result folder new subfolders will be created:
1. MISC - Contains fonts. NOT UNPACKED
2. MISSIONS - Contains tiled .tmx files.
3. MISTEXT* - Contains .txt files with missions texts
4. SPEECH* - Contains speech sounds in wav format.
5. VIDEO - Contains spritesheets in png format.

Example
=========

![](https://github.com/ApmeM/BeastsAndBumpkinsParser/raw/main/Example.png)


Credits
==========

- [**BBTools**](https://github.com/JonMagon/BBTools) - Inspiration and the data was taken from there
- [**Zench from xentax**](https://forum.xen-tax.com/viewtopic.php@p=40921.html#p40921) - C++ parser for m10 files (can be found in BBTools repository above)