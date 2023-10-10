/* 
 
 Image Format Converter
 Developed by Gareth Murfin / Radiant Silver
 ( gareth.murfin@gmail.com  / www.garethmurfin.co.uk )

 What is it ?

 Short: This is basically a project size optimiser, slash your project size on disk by as much as 50%
 by changing huge images like .tga, .bmp, tiff into .png, with no loss in quality.

 Long: Find large image file formats that could be reduced in size with no quality loss.
 tiff, bmp, tga etc are huge, often for no good reason, a png could do the same job at a fraction
 of the size. So you can reduce project size with this as much as 50% or more depending on how many huge 
 relevant files there are.. 
 NOTE that Unity will compress them in its own way when building your game so the build will not reduce in size.
 But doing this reduces my 296 gig project by 74 gigs! You can further use optiPng or similar
 to reduce size of all the pngs even more! (see http://optipng.sourceforge.net/)

 Results? My personal project original size was 296gig, after running this and processing over 8000
 relevant files, the project size was then 222gig that's a 25% size reduction. This allowed me to
 continue working on my laptop which has a small ssd :)

 NOTE, this takes a LONG time if you have a lot of files, it uses a lot of cpu too, it's best to run this 
 when you go out or during the night etc. But it's worth it :) Wake up to a hugely smaller project footprint.
 I have tried to make it similar to light baking, so you can tick 'isRunning' to start and stop it, the idea being
 you can run it whenever you're away from the keyboard, and stop it when you're back.

 Note that even png can still be huge, like 4096x4096 files are still sometimes large (but still tiny compared
 to the source image) because unity wont let you specify depth for the png, so it's always 32 bit (I think, currently).
 In later versions I will add a way for it to call optipng automatically on your computer for further compression! 
 And perhaps to force lower colour depth for huge savings if possible.

 Could we use JPEG? I guess so but I have removed it from this version for the reasons described below:
 For even crazier disk savings you can use JPG, BUT please make sure you're not losing too much quality
 that way - often jpg really is a fine solution because the textures aren't always super important. But to be honest
 I don't use JPEG myself, I value the quality of my source images too much, so beware. Final note on JPEG is that
 it does not support alpha channels, so if you do use it, make sure you aren't destroying materials which are meant
 to have transparency.

 **** VERY VERY IMPORTANT ****
 PLEASE BACKUP YOUR PROJECT BEFORE YOU RUN THIS, IT IS A DESTRUCTIVE OPERATION, YOU CANNOT GET BACK YOUR ORIGINAL
 IMAGE FILES. THINGS SHOULD BE OK, BUT CANNOT BE GUARANTEED (THIS IS A BETA!). I WILL NOT BE HELD RESPONSIBLE FOR 
 ANY LOSS OF DATA. PLEASE CHECK THE RESULTS THROUGHLY BY PLAYING YOUR GAME TO MAKE SURE THE MATERIALS STILL LOOK
 THE SAME. DO NOT WIPE THE BACKUP UNTIL YOUR ARE 110% SURE THAT ALL IS OK. IDEALLY KEEP THE BACKUP FOR AS LONG 
 AS POSSIBLE. THIS IS BETA SOFTWARE AND MAY CONTAIN BUGS, ALWAYS BACK UP YOUR PROJECT FIRST.
 
 This is a commercial asset from the asset store, it is illegal to use this without purchasing. 

 Cheers,
 Gaz.

*/
using UnityEngine;
using UnityEditor;
using System.IO;
using System;
//ADD JPEG JUST WARN HEAVILTY AGAINST IT, MIGHT SAVE SOME ASSES
//REMOVES ALPHA CHANNEL ALSO LET QUALITY BE SUPPPLIED 0-100,
#if UNITY_EDITOR
public class RSL_ConvertImageFormats : ScriptableWizard
{    
    const string VERSION            = "0.7";    
    const int    TARGET_MAX_SIZE    = 8192; //keep this as high as unity can go during the conversion process to preserve quality

    [Header("----- IMAGE FORMAT CONVERTER -----")]
    [Tooltip("This is the size (in Kilobytes) of a file that is considered large enough to process")]
    public int largeSize = 1024; // 1 or more meg is considered big, users can adjust
    [Header("Image formats you wish to convert")]
    [Tooltip("These are the formats that will get converted, they're all traditionally massive for no good reason. Note that .tif also includes .tiff")]
    public string[] largeFormats = new string[] { ".tif", ".tga"/*, ".dds",*/, ".bmp" };//these are the ones we wish to convert (.dds wont work)
    [Header("Platforms to process for")]
    //allow users to specify whatever platform they want
    //TODO: specify multiple platforms? dont think we need to since the source image is being adjusted? but maybe? LEFT THIS FOR NOW.
    string platforms = "Standalone";
    //public string[] platforms  = new string[] { "Standalone", "Web", "iPhone", "Android", "WebGL", "Windows Store Apps", "PS4", "XboxOne", "Nintendo 3DS" and "tvOS" };
       
    [Header("Desired format to convert to")]
    string    desiredFormat = ".png";     //JPEG Support removed, as discussed above (lossy/no alpha)
    [Header("Apply Unity crunched compression to save further disk space")]
    bool      useCrunchedCompression;     //not in use in this version yet. further compress the file on disk, todo: expose this to users?
    string    progressBarMessage;
    int numOfTIFFConverted;
    int numOfTGAConverted;
    int numOfDDSConverted;
    int numOfBMPConverted;

    static bool writerClosed;

    [MenuItem("Window/RadiantSilver/Image Format Converter")]
    static void CreateWizard()
    {
        _("RSL_ConvertImageFormats#### CreateWizard Image Format Converter is born!");

        EditorUtility.DisplayDialog("Image Format Converter",
            "** Welcome! **\n" +
            "** PLEASE BACKUP YOUR PROJECT BEFORE\nYOU RUN THIS **\n",           
            "OK");

        writerClosed = false;
        ScriptableWizard.DisplayWizard<RSL_ConvertImageFormats>("Image Format Converter Wizard (v" + VERSION + ")", "Cancel");//, "OTHER");
    }

    void OnWizardCreate()
    {
        _("OnWizardCreate CANCEL HIT.");
    }

    void OnWizardUpdate()
    {
        helpString =
            "Welcome to IMAGE FORMAT CONVERTER\nby Radiant Silver. \n\n" +
            "USAGE: Please tick 'isRunning' to begin, you can press cancel on the IFC progress bar to stop this operation. You " +
            "can resume at any time (by ticking 'isRunning' again).\n" +
            "This allows you to resume this when you're not using the computer, and pause it when you are (similar to light-baking). \n\n" +
            "Note that all work is logged to RSL_ConvertImageFormats_LOG.txt in Assets/";
        if (isRunning)
        {
            _("RESUME IMAGE FORMAT CONVERTER");
            findAndConvertHugeImages();
        }
        else
        {
            _("PAUSE IMAGE FORMAT CONVERTER");
        }
    }

    private void findAndConvertHugeImages()
    {
        int numFound                            = 0;
        long totalSizeOfAllFoundFiles           = 0;
        bool convertImageFormatForOptimisation  = true; //we flag this when we want to do it for real

        //find all textures in the project
        string[] allTextures = AssetDatabase.FindAssets("t:Texture");
        _("Radiant Silver Image Format Converter " + VERSION);
        _("--------------------------------------------------------");
        _("Texture files found in project :" + allTextures.Length);
        _("these are the ones which are huge images:");
        
		EditorUtility.DisplayDialog("Image Format Converter",
            //"** PLEASE BACKUP YOUR PROJECT BEFORE\nYOU RUN THIS ** \n\n" +
            "Depending on the amount of relevant files this could take a LONG, LONG, LONG time. " +
			"Please run this over night, or before you go out for a delightful evening. " +
            "You can pause and resume the process anytime you like (pause by tapping cancel on the " +
            "IFC progress bar and resume by ticking 'isRunning'). " +
            "This will allow you to let it work while you're away from your computer, " +
            "much like light baking. Also remember that it doesn't need to start again, once a file " +
            "is converted it will be ignored next time. So don't worry about starting and stopping this.",
			"OK");

        // Draw a progressbar to show that work is being done
        float progressBar = 0.0f;

        //go through every texture, and look for large ones in the formats we want to convert
        foreach (string textureTmp in allTextures)
        {
            string fileNameWithPath = AssetDatabase.GUIDToAssetPath(textureTmp);
            //we want to look for the largest image file formats which seem to be tiff, tga, bmp etc
            //are we interested in this file ? ie is a .tga, .tiff etc.
            bool isFileWeWantToProcess = false;
            string formatFound = "";
            for (int i=0; i< largeFormats.Length; i++)
            {
                if (fileNameWithPath.Contains(largeFormats[i]))
                {
                    formatFound = largeFormats[i];
                    isFileWeWantToProcess = true;//if its suffix is in our list then we are interested
                }                    
            }

            if (isFileWeWantToProcess)//is it relevant?
            {
                //we are only interested in it if it is considered large (larger files obviously yield better savings)
                FileInfo fileInfo = new System.IO.FileInfo(fileNameWithPath);
                float fileSizeInKb = fileInfo.Length / 1024;
                if (fileSizeInKb >= largeSize) //yep it's large, let's convert it
                {
                    //work out the size in kilobytes and megabytes
                    float kiloBytes = fileInfo.Length / 1024;
                    float megaBytes = kiloBytes / 1024;
                    //  progressBarMessage = numFound + " Processing Large " + formatFound + " File ( "/* + kiloBytes + "K or " */+ megaBytes + " Megs ) " + fileNameWithPath + " will become a " + desiredFormat;
                    progressBarMessage = numFound + ". Processing " + formatFound + " ("+ roundDecimal(megaBytes) + " Meg) " + fileInfo.Name + " will become a " + desiredFormat;

                    _("Progress Msg-> "+progressBarMessage);
                    numFound++;
                    totalSizeOfAllFoundFiles += fileInfo.Length;
                    progressBar = 50F;
                    if (convertImageFormatForOptimisation)
                    {                       
                        ConvertImageToDesiredFormatAndUpdateName(
                            fileNameWithPath,
                            fileInfo,
                            formatFound,
                            desiredFormat,
                            fileSizeInKb);                       
                        
                        if (formatFound.Equals(".tif"))
                        {
                            numOfTIFFConverted++;
                        }
                        if (formatFound.Equals(".bmp"))
                        {
                            numOfBMPConverted++;
                        }
                        if (formatFound.Equals(".dds"))
                        {
                            numOfDDSConverted++;
                        }
                        if (formatFound.Equals(".tga"))
                        {
                            numOfTGAConverted++;
                        }
                        //_("WAIT.");
                    }
                }                
            }

            //Update progressbar, if they click cancel then isRunning becomes false and the operation is stopped, so users
            //can use it sort of like light baking
            isRunning = !EditorUtility.DisplayCancelableProgressBar("Image Format Converter", progressBarMessage, progressBar);
            if (!isRunning)
            {
                //bail out of entire loop, this allows people to start and stop it when ever like light baking
                //this makes it a much less annoying process, you can start before bed and when you go out etc
                //then stop it when you get back
                _("USER PAUSED IMAGE FORMAT CONVERSION, resume at anytime by ticking isRunning.");
                writer.Close();
                writerClosed = true;
                writer = null;
                break;
            }
        }
        float megs = (totalSizeOfAllFoundFiles / 1024) / 1024;
        float gigs = megs / 1024;
        _("Number of large file found "+ numFound);
        _("total size of all large files is " + megs  + " Megs or "+ roundDecimal(gigs) + " Gigs.");
        AssetDatabase.Refresh();
        _("Image format conversion stopped, please check your materials to see if they're ok.");
        _("Number of TGA files converted: "  + numOfTGAConverted);
        _("Number of DDS files converted: "  + numOfDDSConverted);
        _("Number of BMP files converted: "  + numOfBMPConverted);
        _("Number of TIFF files converted: " + numOfTIFFConverted);
        _("cooked. ALL DONE!!");

        // Remove the progress bar to show that work has finished
        EditorUtility.ClearProgressBar();
        
        megaBytesSaved = kiloBytesSaved / 1024;
        gigaBytesSaved = megaBytesSaved / 1024;

        EditorUtility.DisplayDialog("Image Format Converter", 
			"Image format conversion has stopped (processed " + numFound + " files this session). Please check your project to make sure all is well. " +
            "You have reduced your project size by " +
            ""+ (kiloBytesSaved) +" Kb or "+ roundDecimal(megaBytesSaved) + " Megs or "+ roundDecimal(gigaBytesSaved) + " Gigs. ",
			"OK");

        //reset some stuff
        isRunning           = false; //all done ;)
        gigaBytesSaved      = 0;
        megaBytesSaved      = 0;
        kiloBytesSaved      = 0;
        numOfTIFFConverted  = 0;
        numOfTGAConverted   = 0;
        numOfDDSConverted   = 0;
        numOfBMPConverted   = 0;
        numFound            = 0;
        writerClosed        = false;
    }

    float spaceSaved;
    float kiloBytesSaved;
    float megaBytesSaved;
    float gigaBytesSaved;

    //this will be a checkbox that we can use to start and stop it, we ue a delay to allow this to be ticked
    [Header("Tick 'isRunning' below to resume/start")]
    [Tooltip("Tick this to resume/start")]
    public bool isRunning = false;//so people can stop it and continue it later we need something like this

    //this will take a big image like hello.tga and convert it to hello.png, saving a lot of space,
    //the file and meta are renamed so everything should remain intact. ie materials will be identical in
    //appearance
    private void ConvertImageToDesiredFormatAndUpdateName(
        string fileNameWithPath, 
        FileInfo fileInfo, 
        string detectPrefix, 
        string targetPrefix, 
        float fileSizeInKb)
    {       
        //rename the file, lets choose the new name
        string fileNameWithoutPath = fileInfo.Name;
        //Debug.Log("fileNameWithoutPath--->"+ fileNameWithoutPath);        
        //                                        eg:  .tga         .png
        string newFileName = fileNameWithPath.Replace(detectPrefix, targetPrefix);
        _("Found large "+ detectPrefix + ", will convert to "+ targetPrefix + ", new file name is " + newFileName);
        //this renames it to the right suffix, ie, hello.tga becomes hello.png, the meta also updates which is essential
        //not to wreck the materials they're being used by
        AssetDatabase.MoveAsset(fileNameWithPath, newFileName); //to also rename the meta we use this
        //now we need to genuinely turn it into a png and save it. for that we get the texture importer
        //this is to allow us to change the max size so no quality is lost during conversion
        TextureImporter texImporter = AssetImporter.GetAtPath(newFileName) as TextureImporter;
        
        //in order to make sure we process it at max res (so converted file is the same size)
        //we need to increase its import resolution up to max before we process it, 
        //then we put it back after, this should ensure that the output file is the
        //same resolution as the input file.
        if (texImporter == null) // this can happen for some reason, so we skip any textures that cause this
        {
            _("texImporter is null!! skipping this one!");
            return;
        }
        int initialMaxSize = texImporter.maxTextureSize; //store this for later so we can put it back after
        texImporter.maxTextureSize = TARGET_MAX_SIZE;    // set it to a high res while we process
                
        //What type is this? is it a normal map?
        TextureImporterType tit = texImporter.textureType;
        //_("textureType is "+tit);
        bool isNormalMap = false;
        if (tit.ToString().Equals("NormalMap"))
        {
            isNormalMap = true;
            //if it is a normal map we NEED to turn that off for the conversion, otherwise the result
            //is a weird orange normalmap, presumably because it normal maps it again, so after the conversion we set this back
            texImporter.convertToNormalmap = false;
            texImporter.normalmap = false;            
        }  
     
        //we get the import settings so we can apply them to the new texture 
        TextureImporterPlatformSettings ps = texImporter.GetPlatformTextureSettings(platforms);
        //set the max size for the specific platform (should there be more platforms here? I dont think
        //it matters actually because we are adjusting the png itself.
        ps.maxTextureSize = TARGET_MAX_SIZE;
        texImporter.SetPlatformTextureSettings(ps);
        //Debug.Log("maxTextureSize is " + initialMaxSize + ", increasing it for import to "+ texImporter.maxTextureSize);
        
        //this is essential for the maxTextureSize change to take effect
        texImporter.SaveAndReimport();
        //ok now load the actual image file!
        //this can fail with "specified cast is not invalid" so lets us a try catch so it doesnt bail out
        Texture2D tex = null;
        try
        {
            tex = (Texture2D)AssetDatabase.LoadMainAssetAtPath(newFileName);
        }
        catch (Exception e)
        {
            _("Warning! Exception processing this file, let's skip it. error is "+e.Message);
            return;
        }        
        //Debug.Log("texture loaded from "+ newFileName);

        //we need an uncompressed version, so we use this method to bring it in, since you cannot work
        //with compressed images when encoding in unity
        tex = RSL_ExtensionMethodDecompressTexture2D.decompressTexture(tex);//creates uncompressed version of the image
        //ok now let's encode it to our desired format (such as png or maybe even jpg)
        //remember png is non-lossy, and jpg is lossy so png is recommended, it can also handle alpha etc.
        byte[] bytes = ImageConversion.EncodeToPNG(tex);//convert to new format (todo: add jpg?)
        //tex.
        tex.Compress(true);     //make sure its compressed
        tex.Apply(true,true);   //apply it all.. 
        
        //just check if its readable, it shouldnt be but if it is its slow/memory intensive so we warn them
        if (tex.isReadable)
        {
            _("warning texture is still readable, this will slow things down and use more memory");
        }
        //write the file...
        System.IO.File.WriteAllBytes(newFileName, bytes);
        AssetDatabase.ImportAsset(newFileName); // reimport it..

        //if they want crunch compression, apply it.
        if (useCrunchedCompression)
        {
            ps.crunchedCompression = true;
        }

        if (isNormalMap)
        {
            texImporter.normalmap = true;//turn this back on now the conversion is done
            isNormalMap = false;
        }
        //put it back to the original size so the materials using it look exactly the same etc. ie no higher or lower res, 
        texImporter.maxTextureSize = initialMaxSize;
        texImporter.SetPlatformTextureSettings(ps);//this should set all the original settings, like if its a normal map etc.

        //apply it, we need this or the maxSize stuff doesn't work (we are setting it back to original val)
        texImporter.SaveAndReimport();
        _("Conversion saved to " + newFileName);

        //record how much space was saved so we can present to user
        fileInfo = new System.IO.FileInfo(newFileName);
        float fileSizeInKb_after = fileInfo.Length / 1024;
        spaceSaved += fileSizeInKb - fileSizeInKb_after;

        //_("spaceSaved is now "+ (spaceSaved/1024/1024) + ". fileSizeInKb is "+ (fileSizeInKb / 1024 / 1024) +" and reduced size is "+ (fileSizeInKb_after / 1024 / 1024));
        kiloBytesSaved = spaceSaved;// / 1024;
        //_("kiloBytesSaved becomes "+ (kiloBytesSaved / 1024 / 1024));
    }

    private string roundDecimal(float val)
    {
        return Math.Round(val, 2)+"";
    }

    //for logging to txt file
    static StreamWriter writer;
    static bool logToTextFile = true;
    static bool showDebugOutput = false;
    private static void _(string s)
    {
        if (showDebugOutput)
        {
            Debug.Log("RSL_ConvertImageFormats#### " + s);
        }
        
        //Write some text to the test.txt file
        if (logToTextFile && !writerClosed)
        {
            if (writer == null)
            {
                string path = "Assets/RSL_ConvertImageFormats_LOG.txt";
                writer = new StreamWriter(path, true);
            }
            writer.WriteLine(""+s);
        }        
    }
}
#endif
