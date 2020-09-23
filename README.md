# Extensibility Margin

[![Build status](https://ci.appveyor.com/api/projects/status/4pha1svkn0aqg3u4?svg=true)](https://ci.appveyor.com/project/madskristensen/tweakster)

A collection of minor fixes and tweaks for Visual Studio to reduce the paper cuts and make you a happier developer

Download this extension from the [Marketplace](https://marketplace.visualstudio.com/items?itemName=MadsKristensen.ExtensibilityMargin)
or get the [CI build](https://www.vsixgallery.com/extension/41b4c077-6d9f-4e0a-a356-988baf3e830a).

-----------------------------------------

The margin is located below the bottom scrollbar and comes in handy when writing extensions that extends the VS editor.

# Toggle on/off
Toggle the margin visibility on/off from a new button on the Standard toolbar.

![Margin Toggle](art/margin-toggle.png)

# Bottom margin

![Bottom margin](art/margin.png)

## Document encoding
Shows the encoding of the current document and more details on hover.

![Document encoding](art/margin-encoding.png)

## Content type
Shows the content type of the ITextBuffer at the caret position. The over tooltip shows the name of the base content type.

## Classification
Displays the name of the classification at the caret position in the document. The hover tooltip shows the inheritance hierarchy of the EditorFormatDefinition's BaseDefinition attribute.

![Classifications](art/margin-classification.png)

## Selection
Displays the start and end position of the editor selection as well as the total length of the selection.

![Selection](art/margin-selection.png)

## License
[Apache 2.0](LICENSE)