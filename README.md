# EnchancedChannelPacker
Unity Texture Channels Combiner. Pack multiple texture color channels into one texture. Change strength of each channel, invert. See result on the fly in the editor

# Installation
Just Drag-And-Drop unity package to your project, it imports everything into Plugins/EnchancedChannelPacker folder
Then open window with **Tools/EnchancedChannelPacker**

# Features

Original features:
- Combine multiple textures into one output texture (For use in Mask maps or other packed texture techniques)
- Choose which channel each texture pulls from, and where it goes to
- Invert / multiply texture inputs for desired results
- Have default 0 - 1 values for unassigned texture inputs
- Save multiple presets to have different workflows in the same project
  
My changes:
- Show result texture in default unity's Texture Preview Window. You can select specific channels to see the result before packing texture to the disk
- Update result texture in preview window after any input changes - on input textures reload, on input channel change, on multiplier and invert change
- Remember selected channel in result Texture Preview - now changes in input parameters do not reset it to default
- New Reload Preset button - to forcely reload current preset - in case if it was changed
- Undo/Redo features supported now
- Fixed "Clear All" button - now it clears all field, not just textures

<img width="397" height="1050" alt="Untitled" src="https://github.com/user-attachments/assets/4fd6ae79-aa96-442c-89f1-12845df3172f" />

ChannelPacker is a heavily modified / rewritten version of [MaskPacker](https://www.reddit.com/r/Unity3D/comments/glkvp2/i_made_another_mask_map_packer_for_hdrp/).

EnchancedChannelPacker is lightly modified version of ChannelPacker of [Camobiwon](https://github.com/camobiwon/ChannelPacker).

Thank you original creator and heavily-modifier! This has been extremely useful to me too, and whoever is using this, I hope Enchanced Channel Packer is useful to you as well
Have a nice day!
