using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR   
using UnityEditor;
#endif
using System.IO;
using System;
using Characters;
/// <summary>
/// 引擎运行可以使用,打包出来不能用切记
/// </summary>
public class GameObjToPrefabs : MonoBehaviour
{
    //制作单个的物体预制体
    public void ConvertToPrefab(string name_)
    {
#if UNITY_EDITOR       
        // 将场景物体设为预制体
        if (!Directory.Exists("Assets/AssetBundle/一键生成预制体/" + name_ + "/Prefabs/"))
        {
            Directory.CreateDirectory("Assets/AssetBundle/一键生成预制体/" + name_ + "/Prefabs/");
        }
        UnityEditor.PrefabUtility.SaveAsPrefabAsset(gameObject, "Assets/AssetBundle/一键生成预制体/"+ name_ +"/Prefabs/character.prefab");
#endif

    }
#if UNITY_EDITOR  
    private IEnumerator Shoot()
    {

        string humanName = gameObject.GetComponent<Human>().VisibleName;
        Animation animation = gameObject.GetComponent<Animation>();
        Human human = gameObject.GetComponent<Human>();
        string animationName = "Armature|idle_M";
        if (human.Weapon is AHSSWeapon || human.Weapon is APGWeapon)
        {
            animationName = "Armature|idle_AHSS_M";
        }
        else if (human.Weapon is ThunderspearWeapon)
        {
            animationName = "Armature|idle_TS_M";
        }
        if (animation != null)
        {
            // 播放指定的动画，并跳到第一帧

            animation.Stop(); // 确保动画停止

            AnimationState state = animation[animationName];
            if (state != null)
            {
                state.time = 0f;   // 设置时间为动画的第一帧
                state.speed = 0f;  // 设置速度为 0，确保动画不会继续播放
                animation.Sample(); // 强制应用当前动画状态到物体
            }

            // 播放指定的动画
            animation.Play(animationName);
        }
        yield return new WaitForSeconds(0.2f);
        ConvertToPrefab(humanName);
        saveMat(humanName);
        yield return new WaitForSeconds(1.0f);
        human.forbiddenAnimation = false;
    }
    private void LateUpdate()
    {
        if (Input.GetKeyDown(KeyCode.Keypad0))
        {
            Human human = gameObject.GetComponent<Human>();
            if (human.forbiddenAnimation == false)
            {
                human.forbiddenAnimation = true;
                Debug.Log("shot");
                StartCoroutine(Shoot());
            }
            
        }
    }

    string GenerateRandomFileName()
    {
        // 使用 GUID 生成一个唯一的文件名
        return System.Guid.NewGuid().ToString();
    }


    void saveMat(string name_)
    {
        string materialsFolderPath = "Assets/AssetBundle/一键生成预制体/" + name_ + "/Materials/";
        string texturesFolderPath = "Assets/AssetBundle/一键生成预制体/" + name_ + "/Textures/";
        // 获取当前物体及其所有子物体的Renderer组件
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        
        List<Material> materials = new List<Material>();
        HashSet<Texture> textures = new HashSet<Texture>();  // 用于存储贴图（避免重复）

        // 遍历所有Renderer组件，获取材质和贴图
        foreach (var renderer in renderers)
        {
            foreach (var material in renderer.sharedMaterials)
            {
                if (material != null && !materials.Contains(material))
                {
                    materials.Add(material);
                    
                    // 获取材质中的所有贴图
                    foreach (var property in material.GetTexturePropertyNames())
                    {
                        Texture texture = material.GetTexture(property);
                        if (texture != null && !textures.Contains(texture))
                        {
                            textures.Add(texture);
                        }
                    }
                }
            }
        }

        // 确保文件夹存在
        if (!Directory.Exists(materialsFolderPath))
        {
            Directory.CreateDirectory(materialsFolderPath);
        }
        if (!Directory.Exists(texturesFolderPath))
        {
            Directory.CreateDirectory(texturesFolderPath);
        }

        // 保存材质到指定文件夹
        foreach (var material in materials)
        {
            string materialPath = Path.Combine(materialsFolderPath, material.name.Replace("/", "_") + ".mat");

            // 如果材质还没有被保存过，进行保存
            if (AssetDatabase.GetAssetPath(material) == "")
            {
                AssetDatabase.CreateAsset(new Material(material), materialPath);
            }
        }

        // 保存贴图到指定文件夹
        foreach (var texture in textures)
        {
            string texturePath = Path.Combine(texturesFolderPath, GenerateRandomFileName() + ".png");

            // 检查贴图是否是一个Texture2D，Unity默认保存为PNG格式
            if (texture is Texture2D && texture.isReadable)
            {
                var tex2D = (Texture2D)texture;
                if (tex2D.format == TextureFormat.DXT1 || tex2D.format == TextureFormat.DXT5 || tex2D.format == TextureFormat.ETC_RGB4 || tex2D.format == TextureFormat.ETC2_RGB)
                {
                    // 创建一个新的 Texture2D（未压缩）
                    Texture2D uncompressedTexture = new Texture2D(tex2D.width, tex2D.height, TextureFormat.RGBA32, false);
                    uncompressedTexture.SetPixels(tex2D.GetPixels());  // 获取未压缩的像素数据
                    uncompressedTexture.Apply();

                    // 编码为 PNG
                    byte[] textureBytes = uncompressedTexture.EncodeToPNG();
                    Destroy(uncompressedTexture);  // 释放内存
                    SaveToFile(textureBytes, texturePath);
                }
                else
                {
                    // 直接编码 PNG
                    byte[] textureBytes = tex2D.EncodeToPNG();
                    SaveToFile(textureBytes, texturePath);
                }
            }
        }

        // 刷新AssetDatabase，确保材质和贴图显示在Unity编辑器中
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("所有材质和贴图已保存。");

    }

    void SaveToFile(byte[] textureBytes, string texturePath)
    {
        File.WriteAllBytes(texturePath, textureBytes);

        Debug.Log($"Texture saved to {texturePath}");
    }

#endif
}

