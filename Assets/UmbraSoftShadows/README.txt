**************************************
*         UMBRA SOFT SHADOWS         *
*            by Kronnect             * 
*            README FILE             *
**************************************


Quick help: how to use this asset?
----------------------------------

1. Add "Umbra Render Feature" to the URP Renderer.
2. Add "Umbra Soft Shadows" to the directional light in the scene and customize the settings.


Help & Support Forum
--------------------

Check the Documentation folder for detailed instructions:

Have any question or issue?
* Support-Web: https://kronnect.com/docs/umbra/
* Support-Discord: https://discord.gg/EH2GMaM
* Email: contact@kronnect.com
* Twitter: @Kronnect

If you like Umbra Soft Shadows, please rate it on the Asset Store. It encourages us to keep improving it! Thanks!


Future updates
--------------

All our assets follow an incremental development process by which a few beta releases are published on our support forum (kronnect.com).
We encourage you to signup and engage our forum. The forum is the primary support and feature discussions medium.

Of course, all updates of Umbra will be eventually available on the Asset Store.



More Cool Assets!
-----------------
Check out our other assets here:
https://assetstore.unity.com/publishers/15018



Version history
---------------

Version 10.1
- [Fix] Fixed ImportTexture compatibility with Unity 6 Render Graph

Version 10.0
- Added support for Unity 6.4

Version 7.1
- Added "Force Depth Prepass" option in Advanced Section. Useful to support forward-only materials in deferred rendering path.
- [Fix] Fixes for Unity 6.3

Version 7.0.1
- [Fix] Fixed compatibility with forward opaques in deferred rendering path (also must choose Normals Source = Reconstruct From Depth)

Version 7.0
- Contact shadows: added support for point lights
- More options for constat shadows: depth bias far, edge softness, planar shadows

Version 6.1
- Added early out samples option
- Added option to render only contact shadows
- Added bias option to contact shadows

Version 6.0
- Added "Overlay Shadows" option which allows additional customization options

Version 5.0.1
- Render Graph optimizations

Version 5.0
- Added transparent soft shadows receiver plane support

Versio 4.2.3
- [Fix] Fixes an issue with URP Render Graph in deferred rendering path in Unity 6

Version 4.2.2.1
- [Fix] Fixed missing shadows on surfaces parallel to camera

Version 4.2.2
- Added support for orthographic camera

Version 4.2.1
- [Fix] Fixed contact shadows shader warning message

Version 4.2
- Improved support for multiple directional lights (note: in URP only the brightest directional light can cast shadows)
- [Fix] Fixes an issue with terrain normals

Version 4.1
- Improved contact shadows quality by adding "Vignette Size" and "Normal Bias" options

Version 4.0
- Added Contact Shadows feature

Version 3.0
- Added Render Graph support
- Added Unity native shadows support
- Cascade blending now supports up to 4 cascades

Version 2.0
- Added new options for stylized looks
- Added posterization option to default style with blur
- New optimization options

Version 1.1.2
- [Fix] Fixed shadow intensity issue with blur option

Version 1.1.1
- WebGL/mobile optimizations and fixes

Version 1.1 (May/2024)
- First release
