namespace Coherence.Toolkit.Bindings.ValueBindings
{
    using Entities;
    using UnityEngine;
    using Utils;
    using SimulationFrame;

    public class ByteArrayBinding : ValueBinding<byte[]>
    {
        protected ByteArrayBinding() { }
        public ByteArrayBinding(Descriptor descriptor, Component unityComponent) : base(descriptor, unityComponent)
        {
        }

        public override byte[] Value
        {
            get => (byte[])GetValueUsingReflection();
            set => SetValueUsingReflection(value);
        }

        protected override bool DiffersFrom(byte[] first, byte[] second)
        {
            return first.DiffersFrom(second);
        }

        public override void IsDirty(AbsoluteSimulationFrame simulationFrame, out bool dirty, out bool justStopped)
        {
            justStopped = false;

            var value = Value;

            var changed = DiffersFrom(value, lastCheckedForDirty);
            if (!changed && performedInitialSync)
            {
                dirty = false;

                return;
            }

            if (value != null)
            {
                if (lastCheckedForDirty == null || value.Length != lastCheckedForDirty.Length)
                {
                    lastCheckedForDirty = new byte[value.Length];
                }

                value.CopyTo(lastCheckedForDirty, 0);
            }
            else
            {
                lastCheckedForDirty = null;
            }

            performedInitialSync = true;
            dirty = true;
        }
    }
}
