using System;
using System.Diagnostics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;

using Microsoft.UI.Xaml.Media.Imaging;

using Windows.Graphics.Imaging;
using Windows.Media.Capture;
using Windows.Storage;
using Windows.Storage.Streams;

namespace IPC_Demo;

public static class BlurHelper
{
    /// <summary>
    ///   Applies a simple box blur to a <see cref="BitmapImage"/> and returns a new blurred <see cref="BitmapImage"/>.
    ///   This can be used to obfuscate a user's connection information, or other sensitive data, on screen.
    /// </summary>
    /// <param name="bitmapImage">The input image to blur.</param>
    /// <param name="blurRadius">The blur intensity (higher = more blur).</param>
    /// <returns>A blurred <see cref="BitmapImage"/></returns>
    public static async Task<BitmapImage> ApplyBlurAsync(BitmapImage bitmapImage, int blurRadius = 6)
    {
        // Convert BitmapImage to a SoftwareBitmap
        SoftwareBitmap softwareBitmap = await ConvertBitmapImageToSoftwareBitmapAsync(bitmapImage);

        // Get pixel data
        int width = softwareBitmap.PixelWidth;
        int height = softwareBitmap.PixelHeight;
        byte[] pixelData = new byte[4 * width * height]; // BGRA8 format

        softwareBitmap.CopyToBuffer(pixelData.AsBuffer());

        // Apply box blur effect
        byte[] blurredPixels = ApplyBoxBlur(pixelData, width, height, blurRadius);

        // Create a new SoftwareBitmap from blurred pixels
        SoftwareBitmap blurredBitmap = new SoftwareBitmap(BitmapPixelFormat.Bgra8, width, height, BitmapAlphaMode.Premultiplied);
        blurredBitmap.CopyFromBuffer(blurredPixels.AsBuffer());

        // Convert back to BitmapImage
        return await ConvertSoftwareBitmapToBitmapImageAsync(blurredBitmap);
    }

    /// <summary>
    /// Applies a simple box blur to a <see cref="BitmapImage"/> and returns a new blurred <see cref="BitmapImage"/>.
    /// </summary>
    /// <param name="softwareBitmap">The input image to blur.</param>
    /// <param name="blurRadius">The blur intensity (higher = more blur).</param>
    /// <returns>A blurred <see cref="BitmapImage"/></returns>
    public static async Task<BitmapImage> ApplyBlurAsync(SoftwareBitmap softwareBitmap, int blurRadius = 6)
    {
        // Get the pixel data.
        int width = softwareBitmap.PixelWidth;
        int height = softwareBitmap.PixelHeight;
        byte[] pixelData = new byte[4 * width * height]; // BGRA8 format

        softwareBitmap.CopyToBuffer(pixelData.AsBuffer());

        // Apply box blur effect.
        byte[] blurredPixels = ApplyBoxBlur(pixelData, width, height, blurRadius);

        // Create a new SoftwareBitmap from blurred pixels.
        SoftwareBitmap blurredBitmap = new SoftwareBitmap(BitmapPixelFormat.Bgra8, width, height, BitmapAlphaMode.Premultiplied);
        blurredBitmap.CopyFromBuffer(blurredPixels.AsBuffer());

        // Convert back to BitmapImage.
        return await ConvertSoftwareBitmapToBitmapImageAsync(blurredBitmap);
    }

    /// <summary>
    /// Applies a simple box blur to a <see cref="BitmapImage"/> and returns a new blurred <see cref="SoftwareBitmap"/>.
    /// </summary>
    /// <param name="softwareBitmap">The input image to blur.</param>
    /// <param name="blurRadius">The blur intensity (higher = more blur).</param>
    /// <returns>A blurred <see cref="BitmapImage"/></returns>
    public static SoftwareBitmap ApplyBlur(SoftwareBitmap softwareBitmap, int blurRadius = 6)
    {
        // Get the pixel data.
        int width = softwareBitmap.PixelWidth;
        int height = softwareBitmap.PixelHeight;
        byte[] pixelData = new byte[4 * width * height]; // BGRA8 format

        softwareBitmap.CopyToBuffer(pixelData.AsBuffer());

        // Apply box blur effect.
        byte[] blurredPixels = ApplyBoxBlur(pixelData, width, height, blurRadius);

        // Create a new SoftwareBitmap from blurred pixels.
        SoftwareBitmap blurredBitmap = new SoftwareBitmap(BitmapPixelFormat.Bgra8, width, height, BitmapAlphaMode.Premultiplied);
        blurredBitmap.CopyFromBuffer(blurredPixels.AsBuffer());

        // Convert back to BitmapImage.
        return blurredBitmap;
    }

    /// <summary>
    /// Applies a simple box blur to a <see cref="BitmapImage"/> and saves the <see cref="SoftwareBitmap"/> to disk.
    /// </summary>
    /// <param name="softwareBitmap">The input image to blur.</param>
    /// <param name="blurRadius">The blur intensity (higher = more blur).</param>
    /// <param name="filePath">The output path to save.</param>
    /// <returns>A blurred <see cref="BitmapImage"/></returns>
    public static async Task<bool> ApplyBlurAndSaveAsync(SoftwareBitmap softwareBitmap, string filePath, int blurRadius = 6)
    {
        // Get the pixel data.
        int width = softwareBitmap.PixelWidth;
        int height = softwareBitmap.PixelHeight;
        byte[] pixelData = new byte[4 * width * height]; // BGRA8 format

        softwareBitmap.CopyToBuffer(pixelData.AsBuffer());

        // Apply box blur effect.
        byte[] blurredPixels = ApplyBoxBlur(pixelData, width, height, blurRadius);

        // Create a new SoftwareBitmap from blurred pixels.
        SoftwareBitmap blurredBitmap = new SoftwareBitmap(BitmapPixelFormat.Bgra8, width, height, BitmapAlphaMode.Premultiplied);
        blurredBitmap.CopyFromBuffer(blurredPixels.AsBuffer());

        if (string.IsNullOrEmpty(filePath))
            return false;

        return await SaveSoftwareBitmapToFileAsync(blurredBitmap, filePath);
    }

    /// <summary>
    /// Applies a simple box blur algorithm to pixel data.
    /// </summary>
    static byte[] ApplyBoxBlur(byte[] pixels, int width, int height, int radius)
    {
        byte[] result = new byte[pixels.Length];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int r = 0, g = 0, b = 0, a = 0, count = 0;
                // Iterate over neighboring pixels
                for (int dy = -radius; dy <= radius; dy++)
                {
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        int nx = x + dx, ny = y + dy;
                        if (nx >= 0 && nx < width && ny >= 0 && ny < height)
                        {
                            int index = (ny * width + nx) * 4;
                            b += pixels[index + 0]; // Blue
                            g += pixels[index + 1]; // Green
                            r += pixels[index + 2]; // Red
                            a += pixels[index + 3]; // Alpha
                            count++;
                        }
                    }
                }
                // Compute average color
                int outputIndex = (y * width + x) * 4;
                result[outputIndex + 0] = (byte)(b / count);
                result[outputIndex + 1] = (byte)(g / count);
                result[outputIndex + 2] = (byte)(r / count);
                result[outputIndex + 3] = (byte)(a / count);
            }
        }
        return result;
    }

    /// <summary>
    /// Converts a BitmapImage to a SoftwareBitmap.
    /// </summary>
    public static async Task<SoftwareBitmap> ConvertBitmapImageToSoftwareBitmapAsync(BitmapImage bitmapImage)
    {
        using (InMemoryRandomAccessStream stream = new InMemoryRandomAccessStream())
        {
            BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
            BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);
            return await decoder.GetSoftwareBitmapAsync();
        }
    }

    /// <summary>
    /// Converts a SoftwareBitmap to a BitmapImage.
    /// </summary>
    public static async Task<BitmapImage> ConvertSoftwareBitmapToBitmapImageAsync(SoftwareBitmap softwareBitmap)
    {
        using (InMemoryRandomAccessStream stream = new InMemoryRandomAccessStream())
        {
            BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
            encoder.SetSoftwareBitmap(softwareBitmap);
            await encoder.FlushAsync();
            BitmapImage bitmapImage = new BitmapImage();
            await bitmapImage.SetSourceAsync(stream);
            return bitmapImage;
        }
    }

    public static async Task<SoftwareBitmap> LoadFromFile(StorageFile file)
    {
        SoftwareBitmap softwareBitmap;
        using (IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.Read))
        {
            BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);
            softwareBitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Rgba8, BitmapAlphaMode.Premultiplied);
        }
        return softwareBitmap;
    }

    /// <summary>
    ///   Uses a <see cref="Windows.Graphics.Imaging.BitmapEncoder"/> to save the output.
    /// </summary>
    /// <param name="softwareBitmap"><see cref="Windows.Graphics.Imaging.SoftwareBitmap"/></param>
    /// <param name="filePath">output file path to save</param>
    /// <param name="interpolation">In general, moving from NearestNeighbor to Fant, interpolation quality increases while performance decreases.</param>
    /// <remarks>
    ///   Assumes <see cref="Windows.Graphics.Imaging.BitmapEncoder.PngEncoderId"/>.
    ///   [Windows.Graphics.Imaging.BitmapInterpolationMode]
    ///   3 Fant...........: A Fant resampling algorithm. Destination pixel values are computed as a weighted average of the all the pixels that map to the new pixel in a box shaped kernel.
    ///   2 Cubic..........: A bicubic interpolation algorithm. Destination pixel values are computed as a weighted average of the nearest sixteen pixels in a 4x4 grid.
    ///   1 Linear.........: A bilinear interpolation algorithm. The output pixel values are computed as a weighted average of the nearest four pixels in a 2x2 grid.
    ///   0 NearestNeighbor: A nearest neighbor interpolation algorithm. Also known as nearest pixel or point interpolation. The output pixel is assigned the value of the pixel that the point falls within. No other pixels are considered.
    /// </remarks>
    public static async Task<bool> SaveSoftwareBitmapToFileAsync(Windows.Graphics.Imaging.SoftwareBitmap softwareBitmap, string filePath, Windows.Graphics.Imaging.BitmapInterpolationMode interpolation = Windows.Graphics.Imaging.BitmapInterpolationMode.NearestNeighbor, int maxRetries = 5)
    {
        int attempt = 0;

        if (System.IO.File.Exists(filePath))
        {
            while (attempt < maxRetries)
            {
                try
                {
                    Windows.Storage.StorageFile file = await Windows.Storage.StorageFile.GetFileFromPathAsync(filePath);
                    using (Windows.Storage.Streams.IRandomAccessStream stream = await file.OpenAsync(Windows.Storage.FileAccessMode.ReadWrite))
                    {
                        BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
                        encoder.SetSoftwareBitmap(softwareBitmap);
                        encoder.BitmapTransform.InterpolationMode = interpolation;
                        await encoder.FlushAsync();
                        return true; // Success
                    }
                }
                catch (UnauthorizedAccessException ex)
                {
                    attempt++;
                    Debug.WriteLine($"[ERROR] UnauthorizedAccessException - Retry {attempt}/{maxRetries}: {ex.Message}");
                    if (attempt >= maxRetries)
                    {
                        Debug.WriteLine("[ERROR] Max retries reached. Giving up.");
                        return false;
                    }
                    await Task.Delay(150); // Wait before retrying
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ERROR] SaveSoftwareBitmapToFileAsync({ex.HResult}): {ex.Message}");
                    return false;
                }
            }
        }
        else
        {
            // Get the folder and file name from the file path.
            string? folderPath = System.IO.Path.GetDirectoryName(filePath);
            string? fileName = System.IO.Path.GetFileName(filePath);
            // Create the folder if it does not exist.
            Windows.Storage.StorageFolder storageFolder = await Windows.Storage.StorageFolder.GetFolderFromPathAsync(folderPath);
            Windows.Storage.StorageFile file = await storageFolder.CreateFileAsync(fileName, Windows.Storage.CreationCollisionOption.ReplaceExisting);
            using (Windows.Storage.Streams.IRandomAccessStream stream = await file.OpenAsync(Windows.Storage.FileAccessMode.ReadWrite))
            {
                Windows.Graphics.Imaging.BitmapEncoder encoder = await Windows.Graphics.Imaging.BitmapEncoder.CreateAsync(Windows.Graphics.Imaging.BitmapEncoder.PngEncoderId, stream);
                encoder.SetSoftwareBitmap(softwareBitmap);
                encoder.BitmapTransform.InterpolationMode = interpolation;
                try
                {
                    await encoder.FlushAsync();
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ERROR] SaveSoftwareBitmapToFileAsync({ex.HResult}): {ex.Message}");
                }
            }
        }
        return false;
    }

    public static async Task<SoftwareBitmap?> LoadSoftwareBitmapFromUriAsync(Uri uri)
    {
        try
        {
            StorageFile file = await StorageFile.GetFileFromApplicationUriAsync(uri);
            using (IRandomAccessStream stream = await file.OpenReadAsync())
            {
                BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);
                SoftwareBitmap softwareBitmap = await decoder.GetSoftwareBitmapAsync();
                return softwareBitmap;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ERROR] LoadSoftwareBitmapFromUriAsync: {ex.Message}");
            return null;
        }
    }

    public static async Task<SoftwareBitmap?> LoadSoftwareBitmapFromPathAsync(string path)
    {
        try
        {
            StorageFile file = await StorageFile.GetFileFromPathAsync(path);
            using (IRandomAccessStream stream = await file.OpenReadAsync())
            {
                BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);
                SoftwareBitmap softwareBitmap = await decoder.GetSoftwareBitmapAsync();
                return softwareBitmap;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ERROR] LoadSoftwareBitmapFromPathAsync: {ex.Message}");
            return null;
        }
    }

}

/// <summary>
/// Sample method for testing blur helper.
/// </summary>
//async void ApplyBoxBlurTest()
//{
//    // Load image from assets.
//    BitmapImage originalImage = new BitmapImage(new Uri("ms-appx:///Assets/AppIcon.png"));
//    // Apply blur
//    BitmapImage blurredImage = await ImageProcessingHelper.ApplyHomeBrewBlurAsync(originalImage, blurRadius: 5);
//    // Display the blurred image in an Image control
//    MyImageControl.Source = blurredImage;
//}
