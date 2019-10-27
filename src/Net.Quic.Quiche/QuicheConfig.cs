using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Net.Quic.Quiche
{
    /// <summary>
    /// Opaque handle to Quiche Configuration. Cannot be modified once built but can be reused for different Quiche operations.
    /// </summary>
    public class QuicheConfig: SafeHandle
    {
        private IntPtr _handle;

        public QuicheConfig(IntPtr handle): base(handle, ownsHandle: true)
        {
        }

        public override bool IsInvalid => _handle == IntPtr.Zero;

        protected override bool ReleaseHandle()
        {
            NativeMethods.quiche_config_free(_handle);
            return true;
        }
    }
}
