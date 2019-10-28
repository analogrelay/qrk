using System;
using System.Runtime.InteropServices;

namespace Net.Quic.Quiche
{
    /// <summary>
    /// Opaque handle to Quiche Configuration. Cannot be modified once built but can be reused for different Quiche operations.
    /// </summary>
    public class QuicheConfig: SafeHandle
    {
        public QuicheConfig(IntPtr handle): base(handle, ownsHandle: true)
        {
        }

        public override bool IsInvalid => handle == IntPtr.Zero;

        protected override bool ReleaseHandle()
        {
            NativeMethods.quiche_config_free(handle);
            return true;
        }
    }
}
