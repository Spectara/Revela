using System.Buffers.Binary;
using System.Text;
using NetVips;
using Image = NetVips.Image;

namespace Spectara.Revela.Tests.Shared.Fixtures;

/// <summary>
/// ICC profiles for colour-management tests.
/// </summary>
public static class TestIccProfiles
{
    /// <summary>libvips metadata field holding the embedded ICC profile.</summary>
    public const string ProfileField = "icc-profile-data";

    /// <summary>
    /// Bytes of a libvips built-in profile (<c>"srgb"</c>, <c>"p3"</c>, <c>"cmyk"</c>).
    /// </summary>
    public static byte[] BuiltIn(string name)
    {
        using var pixel = Image.Black(1, 1, bands: 3).Cast(Enums.BandFormat.Uchar).Copy(interpretation: Enums.Interpretation.Srgb);
        using var tagged = pixel.IccTransform(name, inputProfile: "srgb");
        return (byte[])tagged.Get(ProfileField);
    }

    /// <summary>
    /// An ICC v2 matrix/TRC display profile with the Adobe RGB (1998) primaries, white point and gamma.
    /// </summary>
    /// <remarks>
    /// Built in code so the tests need no profile file: colour-equivalent to Adobe RGB (1998)
    /// (D50-adapted colorants, gamma 563/256), but not Adobe's file.
    /// </remarks>
    public static byte[] AdobeRgbCompatible()
    {
        const string profileName = "Adobe RGB (1998) compatible (Revela tests)";
        // textDescriptionType: ASCII text, then empty Unicode (8 bytes) and ScriptCode (3 + 67 bytes) parts.
        var description = new byte[12 + profileName.Length + 1 + 78];
        Encoding.ASCII.GetBytes("desc").CopyTo(description, 0);
        BinaryPrimitives.WriteUInt32BigEndian(description.AsSpan(8), (uint)profileName.Length + 1);
        Encoding.ASCII.GetBytes(profileName).CopyTo(description, 12);

        var copyright = Encoding.ASCII.GetBytes("text\0\0\0\0No copyright\0");

        // One gamma value (u8Fixed8): 563/256 = 2.19921875.
        var curve = new byte[14];
        Encoding.ASCII.GetBytes("curv").CopyTo(curve, 0);
        BinaryPrimitives.WriteUInt32BigEndian(curve.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt16BigEndian(curve.AsSpan(12), 563);

        (string Signature, byte[] Data)[] tags =
        [
            ("desc", description),
            ("cprt", copyright),
            ("wtpt", Xyz(0.9642, 1.0, 0.8249)),
            ("rXYZ", Xyz(0.6097559, 0.3111242, 0.0194811)),
            ("gXYZ", Xyz(0.2052401, 0.6256560, 0.0608902)),
            ("bXYZ", Xyz(0.1492240, 0.0632197, 0.7448387)),
            ("rTRC", curve),
            ("gTRC", curve),
            ("bTRC", curve),
        ];

        var dataStart = 128 + 4 + (tags.Length * 12);
        var offsets = new int[tags.Length];
        var size = dataStart;
        for (var i = 0; i < tags.Length; i++)
        {
            size = (size + 3) & ~3;
            offsets[i] = size;
            size += tags[i].Data.Length;
        }

        var profile = new byte[size];
        BinaryPrimitives.WriteUInt32BigEndian(profile, (uint)size);
        BinaryPrimitives.WriteUInt32BigEndian(profile.AsSpan(8), 0x02100000);
        Encoding.ASCII.GetBytes("mntrRGB XYZ ").CopyTo(profile, 12);
        Encoding.ASCII.GetBytes("acsp").CopyTo(profile, 36);
        WriteS15Fixed16(profile, 68, 0.9642);
        WriteS15Fixed16(profile, 72, 1.0);
        WriteS15Fixed16(profile, 76, 0.8249);

        BinaryPrimitives.WriteUInt32BigEndian(profile.AsSpan(128), (uint)tags.Length);
        for (var i = 0; i < tags.Length; i++)
        {
            var entry = 132 + (i * 12);
            Encoding.ASCII.GetBytes(tags[i].Signature).CopyTo(profile, entry);
            BinaryPrimitives.WriteUInt32BigEndian(profile.AsSpan(entry + 4), (uint)offsets[i]);
            BinaryPrimitives.WriteUInt32BigEndian(profile.AsSpan(entry + 8), (uint)tags[i].Data.Length);
            tags[i].Data.CopyTo(profile, offsets[i]);
        }

        return profile;

        static byte[] Xyz(double x, double y, double z)
        {
            var tag = new byte[20];
            Encoding.ASCII.GetBytes("XYZ ").CopyTo(tag, 0);
            WriteS15Fixed16(tag, 8, x);
            WriteS15Fixed16(tag, 12, y);
            WriteS15Fixed16(tag, 16, z);
            return tag;
        }

        static void WriteS15Fixed16(byte[] buffer, int offset, double value) =>
            BinaryPrimitives.WriteInt32BigEndian(buffer.AsSpan(offset), (int)Math.Round(value * 65536));
    }
}
