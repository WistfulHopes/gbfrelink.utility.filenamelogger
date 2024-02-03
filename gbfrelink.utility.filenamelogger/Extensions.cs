using System.Runtime.CompilerServices;

namespace gbfrelink.utility.filenamelogger;

public static class Extensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string BytesToString(this byte[] buffer) {
        int end;

        for (end = 0; end < buffer.Length && buffer[end] != 0; end++);

        unsafe {
            fixed (byte* pinnedBuffer = buffer) {
                return new((sbyte*)pinnedBuffer, 0, end);
            }
        }
    }
}