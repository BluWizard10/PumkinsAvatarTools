﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Pumkin.DataStructures;
using Pumkin.Translations;
using System.Linq;
using UnityEditor;
using System;
using Pumkin.AvatarTools;
using System.IO;
using UnityEditor.Presets;
using Pumkin.HelperFunctions;
using Pumkin.Dependencies;

public static class PumkinsLanguageManager
{    
    static readonly string resourceTranslationPath = "Translations/";
    public static readonly string translationFullPath = PumkinsAvatarTools.ResourceFolderPath + '/' + resourceTranslationPath;        
    
    static List<PumkinsTranslation> _languages = new List<PumkinsTranslation>();    

    public static List<PumkinsTranslation> Languages
    {
        get
        {            
            return _languages;
        }
        private set
        {
            _languages = value;
        }
    }

    static string translationScriptGUID;
    
    public static void LoadTranslations()
    {
        var guids = AssetDatabase.FindAssets(typeof(PumkinsTranslation).Name);
        translationScriptGUID = guids[0];
                
        foreach(var lang in Languages)
        {
            if(!Helpers.IsAssetInAssets(lang))
                Helpers.DestroyAppropriate(lang);
        }

        LoadTranslationPresets();

        var def = PumkinsTranslation.GetOrCreateDefaultTranslation();
        if(Languages.Count == 0 || !def.Equals(Languages[0]))
            Languages.Insert(0, PumkinsTranslation.Default);

        var loaded = Resources.LoadAll<PumkinsTranslation>(resourceTranslationPath);

        foreach(var l in loaded)
        {
            int i = Languages.IndexOf(l);
            if(i != -1)
                Languages[i] = l;
            else
                Languages.Add(l);
        }

        string langs = "Loaded languages: {";
        for(int i = 0; i < Languages.Count; i++)
        {
            langs += (" " + Languages[i].languageName);
            langs += (i != Languages.Count - 1) ? "," : "";
        }
        langs += " }";
        PumkinsAvatarTools.LogVerbose(langs);
    }

    private static void LoadTranslationPresets()
    {
        var pres = Resources.LoadAll<Preset>(resourceTranslationPath);
        var trans = Resources.LoadAll<PumkinsTranslation>(resourceTranslationPath);

        //AssetDatabase.FindAssets('*.preset', resourceTranslationPath

        //Preset playerSettings = AssetDatabase.LoadAssetAtPath<Preset>("Assets/Presets/Settings_Player.preset");
        //playerSettings.ApplyTo(PlayerSettings./*no object reference to apply to*/)

        foreach(var p in pres)
        {
            var mods = p.PropertyModifications;
            string langName = mods.FirstOrDefault(m => m.propertyPath == "languageName").value;
            string author = mods.FirstOrDefault(m => m.propertyPath == "author").value;

            if(Helpers.StringIsNullOrWhiteSpace(langName) || Helpers.StringIsNullOrWhiteSpace(author))
            {
                PumkinsAvatarTools.Log(Strings.Log.invalidTranslation, LogType.Error, p.name);
                continue;
            }

            var tr = trans.FirstOrDefault(t => t.author == author && t.languageName == langName);
            if(tr == default)
                tr = ScriptableObjectUtility.CreateAndSaveAsset<PumkinsTranslation>("language_" + langName, translationFullPath);

            if(p.CanBeAppliedTo(tr))
                p.ApplyTo(tr);
            else
                PumkinsAvatarTools.Log("Can't apply preset", LogType.Warning);
        }
    }

    public static int GetIndexOfLanguage(string nameAndAuthor)
    {
        for(int i = 0; i < Languages.Count; i++)
        {
            if(Languages[i] != null && Languages[i].ToString().ToLower() == nameAndAuthor.ToLower())
                return i;
        }
        return 0;
    }

    public static PumkinsTranslation FindLanguageFile(string nameAndAuthor)
    {
        string s = nameAndAuthor.ToLower();
        return Languages.FirstOrDefault(o => o.ToString().ToLower() == s);
    }    

    public static void SetLanguage(PumkinsTranslation translationFile)
    {
        Strings.Translation = translationFile;
    }

    public static void SetLanguage(string languageName, string author)
    {
        if(string.IsNullOrEmpty(languageName))
            return;
        SetLanguage((languageName + " - " + author).ToLower());
    }

    public static void SetLanguage(string languageAndAuthor)
    {
        if(string.IsNullOrEmpty(languageAndAuthor))
            return;
        var s = languageAndAuthor.ToLower();
        for(int i = 0; i < Languages.Count; i++)
        {
            if(Languages[i] && s == Languages[i].ToString().ToLower())
            {
                SetLanguage(Languages[i]);
                return;
            }
        }
        SetLanguage(PumkinsTranslation.Default);
    }

    public static bool LanguageExists(PumkinsTranslation translation)
    {
        if (translation == null)
            return false;
        return LanguageExists(translation.languageName, translation.author);
    }

    public static bool LanguageExists(string languageName, string author)
    {
        if (Helpers.StringIsNullOrWhiteSpace(languageName) || Helpers.StringIsNullOrWhiteSpace(author))
            return false;
        var lang = Languages.FirstOrDefault(l => (l.author == author) && (l.languageName == languageName));
        return lang != default(PumkinsTranslation) ? true : false;
    }

    public static void ImportLanguageAsset()
    {
        var filterStrings = ExtensionStrings.GetFilterString(typeof(PumkinsTranslation));
        string filePath = EditorUtility.OpenFilePanelWithFilters("Pick a Translation", "", filterStrings);

        ImportLanguageAsset(filePath);
    }

    public static void ImportLanguageAsset(string path)
    {
        if(Helpers.StringIsNullOrWhiteSpace(path))
            return;

        string newPath = translationFullPath + Path.GetFileName(path);

        bool shouldDelete = false;
        if(File.Exists(newPath))
            if(EditorUtility.DisplayDialog(Strings.Warning.warn, Strings.Warning.languageAlreadyExistsOverwrite, Strings.Buttons.ok, Strings.Buttons.cancel))
                shouldDelete = true;

        if(!Helpers.PathsAreEqual(path, newPath))
        {
            if(shouldDelete)
                File.Delete(newPath);
            File.Copy(path, newPath);
        }
        ReplaceTranslationPresetGUIDTemp(newPath, translationScriptGUID);

        string newPathLocal = Helpers.PathToLocalAssetsPath(newPath);
        AssetDatabase.ImportAsset(newPathLocal);

        LoadTranslations();
    }

    /// <summary>
    /// Doesn't work for now
    /// </summary>    
    static void ReplaceTranslationPresetGUID(string filePath, string newGUID)
    {
        using(var readerStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Write))
        using(var reader = new StreamReader(readerStream))
        using(var writerStream = new FileStream(filePath, FileMode.Open, FileAccess.Write, FileShare.Read))
        using(var writer = new StreamWriter(writerStream, reader.CurrentEncoding))
        {
            string line;
            while((line = reader.ReadLine()) != null)
            {                
                if(!line.Contains("m_ManagedTypePPtr"))
                    continue;

                string search = "guid: ";
                int guidStart = line.IndexOf(search) + search.Length;                    
                line = line.Remove(guidStart) + newGUID + ",";                
                writer.WriteLine(line);
                break;
            }
        }
    }

    /// <summary>
    /// TODO: Replace with one that reads only the needed lines
    /// </summary>    
    static void ReplaceTranslationPresetGUIDTemp(string filePath, string newGUID)
    {
        var lines = File.ReadAllLines(filePath);
        for(int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            
            if(!line.Contains("m_ManagedTypePPtr"))
                continue;
            
            string search = "guid: ";
            int guidStart = line.IndexOf(search) + search.Length;
            line = line.Remove(guidStart) + newGUID + ",";
            lines[i] = line;
            break;
        }
        
        File.WriteAllLines(filePath, lines);
    }
}

