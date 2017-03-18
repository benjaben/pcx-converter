using UnityEngine;
using System;
using System.Collections;
using System.IO;

public class PCXImageParser : MonoBehaviour 
{
	public string path = "";
	private MeshRenderer meshRendererComponent;//cached mesh renderer

	private void Awake()
	{
		meshRendererComponent = GetComponent<MeshRenderer>();
	}

	private void Start()
	{
		//Create the header struct
		ImageHeader header = new ImageHeader();
		header.Setup(File.OpenRead(Application.dataPath + path));

		//Create the palette, image and colors arrays
		byte[] palette = GetPalette(File.OpenRead(Application.dataPath + path));
		byte[] image = DecodeImage(header.bytesPerLine, header.yEnd + 1, path);
		Pixel[] colors = new Pixel[256];

		int index = 0;//index of each RGB tuple

		//Get the pixels from the pallette
		for(int i = 0; i < colors.Length; i++)
		{
			colors[i] = new Pixel(palette[index], palette[index + 1], palette[index + 2]);
			index += 3;
		}

		//There is no padding in this file, so i'll leave this out
		//int padding = ((header.bytesPerLine * header.numBitPlanes) * (8 / header.bitsPerPixel)) - ((header.xEnd - header.xStart) + 1);

		//Create a texture from the pixels
		Texture2D tex = new Texture2D(header.bytesPerLine, header.bytesPerLine);
		CreateTextureOptimized(image, colors, tex);

		//Set the texture to the renderer
		meshRendererComponent.material.mainTexture = tex;
	}

	/// <summary>
	/// Creates a texture from the image array, the pixels and applies it the Texture2D passed in
	/// </summary>
	/// <param name="image"></param>
	/// <param name="colors"></param>
	/// <param name="tex"></param>
	private static void CreateTexture(byte[] image, Pixel[] colors, Texture2D tex)
	{
		byte[,] pixels2D = new byte[tex.width, tex.height];//2d map of the image data

		for(int i = 0; i < image.Length; i++)//Setup the pixel2d array by using the image array
		{
			pixels2D[Mathf.FloorToInt(i / tex.width), i % tex.width] = image[i];
		}

		for(int i = 0; i < tex.width; i++)
		{
			for(int j = 0; j < tex.height; j++)
			{
				Pixel pixel = colors[pixels2D[i, j]];
				tex.SetPixel(i, j, new Color((float)pixel.r / 255, (float)pixel.g / 255, (float)pixel.b / 255));//set each individual pixel
			}
		}

		tex.Apply();//save the texture
	}

	//This is the more optimized version (no 2d array for pixels2D)
	private static void CreateTextureOptimized(byte[] image, Pixel[] colors, Texture2D tex)
	{
		for(int i = 0; i < tex.width; i++)
		{
			for(int j = 0; j < tex.height; j++)
			{
				Pixel pixel = colors[image[i * tex.width + j]];
				tex.SetPixel(i, j, new Color((float)pixel.r / 255, (float)pixel.g / 255, (float)pixel.b / 255));//set each individual pixel
			}
		}

		tex.Apply();//save the texture
	}

	/// <summary>
	/// Converts the image into a non-compressed format
	/// </summary>
	/// <param name="bytesPerScanline"></param>
	/// <param name="scanlines"></param>
	/// <param name="path"></param>
	/// <returns></returns>
	public byte[] DecodeImage(int bytesPerScanline, int scanlines, string path)
	{
        byte[] data = new Byte[bytesPerScanline * scanlines]; //BytesPerScanline * ScanLines

        BinaryReader r = new BinaryReader(File.OpenRead(Application.dataPath + path));
        r.BaseStream.Seek(128, SeekOrigin.Begin); //Moves the reader past the header file

		int repeatCount;
        byte readByte;

        int row = 0;
        int column = 0;
		 
        while(row < scanlines)//iterate through the lines
        {
            readByte = r.ReadByte();

			//TODO: Move this down into Compressed data
            repeatCount = (readByte & 0x3F);//get the number of times the pixel is repeated

            if(!(column >= bytesPerScanline))//Dont go over the line buffer
            {
                if(0xC0 == (readByte & 0xC0))//Compressed data
                {
                    readByte = r.ReadByte();

                    while(repeatCount > 0)//iterate through the compressed values
                    {
                        data[(row * bytesPerScanline) + column] = readByte;
                        repeatCount -= 1;
                        column += 1;
                    }
                }
                else//Non compressed data
                {
                    data[(row * bytesPerScanline) + column] = readByte;
                    column += 1;
                }
            }

            if(column >= bytesPerScanline)//Next row
            {
                column = 0;
                row += 1;
            }
        }

       return data;
	}

	/// <summary>
	/// Creates a palette byte array from the image file
	/// </summary>
	/// <param name="file"></param>
	/// <returns></returns>
	public byte[] GetPalette(Stream file)
	{
		BinaryReader reader = new BinaryReader(file);
		byte[] palette = new byte[768];

		reader.BaseStream.Seek(-768, SeekOrigin.End);//Move the reader to the start of the pallette

		for(int i = 0; i < 768; i++)
		{
			palette[i] = reader.ReadByte();
		}

		return palette;
	}
}

/// <summary>
/// A struct that contains all the header information for the image
/// </summary>
struct ImageHeader
{
	public int identifier;
	public int version;
	public int encoding;
	public int bitsPerPixel;
	public short xStart;
	public short yStart;
	public short xEnd;
	public short yEnd;
	public short horizontalResolution;
	public short verticalResolution;
	public byte[] egaPallete;
	public int numBitPlanes;
	public short bytesPerLine;
	public short palleteType;
	public short horizontalScreenSize;
	public short verticalScreenSize;

	public void Setup(Stream file)
	{
		BinaryReader reader = new BinaryReader(file);

		identifier = reader.ReadByte();
		version = reader.ReadByte();
		encoding = reader.ReadByte();
		bitsPerPixel = reader.ReadByte();
		xStart = reader.ReadInt16();
		yStart = reader.ReadInt16();
		xEnd = reader.ReadInt16();
		yEnd = reader.ReadInt16();
		horizontalResolution = reader.ReadInt16();
		verticalResolution = reader.ReadInt16();
		egaPallete = reader.ReadBytes(48);
		reader.ReadByte();
		numBitPlanes = reader.ReadByte();
		bytesPerLine = reader.ReadInt16();
		palleteType = reader.ReadInt16();
		horizontalScreenSize = reader.ReadInt16();
		verticalResolution = reader.ReadInt16();
	}
}

/// <summary>
/// Pixel struct to contain the RGB values
/// </summary>
struct Pixel
{
	public byte r, g, b;

	public Pixel(byte r, byte g, byte b)
	{
		this.r = r;
		this.g = g;
		this.b = b;
	}
}