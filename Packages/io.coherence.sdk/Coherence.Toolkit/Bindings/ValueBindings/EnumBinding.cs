namespace Coherence.Toolkit.Bindings.ValueBindings
{
    using UnityEngine;

    public class EnumBinding : ValueBinding<int>
    {
        protected EnumBinding() { }
        public EnumBinding(Descriptor descriptor, Component unityComponent) : base(descriptor, unityComponent)
        {
        }

        public override int Value
        {
            get => (int)GetValueUsingReflection();
            set => SetValueUsingReflection(value);
        }

        protected override bool DiffersFrom(int first, int second)
        {
            return first != second;
        }
    }
}
