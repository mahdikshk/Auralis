using System;
using System.Collections.Generic;
using System.Text;

namespace Auralis.Engine;

public abstract class ComponentBase
{
    public Guid Id { get; set; }
    private string _name = string.Empty;
    public virtual string Name
    {
        get
        {
            return _name;
        }
        set
        {
            if(value == _name)
                return;
            _name = value;
            OnNameChanged();
        }
    }

    protected virtual void OnNameChanged()
    {

    }
    protected virtual void OnDestroy()
    {

    }
    public void Destroy(ComponentBase component)
    {
        component?.OnDestroy();
    }
}
