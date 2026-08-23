using System;
using System.Collections.Generic;
using System.Text;

namespace Auralis.Tools.AssetPipeline.Interchange.FBX;

public class FBXNode
{
    private readonly List<FBXNode> _children = [];
    public string Name { get; private set; } = "";
    public FBXNode()
    {

    }
    public FBXNode(string name)
    {
        Name = name;
    }

    public void AddChild(FBXNode child)
    {
        _children.Add(child);
    }

}
