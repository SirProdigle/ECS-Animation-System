using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Prutils.SpriteAnimation
{

    [UpdateAfter(typeof(AnimationSystem))]
    [DisableAutoCreation]
    public class SpriteSheetRenderer_OLD : ComponentSystem
    {
        //TODO Basically re-do this code once per different sprite tag, e.g once for User and once for Goblins, then we do 1 draw call per spritesheet
        protected override void OnUpdate() {
            //Get our entity reference
            EntityManager entityManager = World.Active.EntityManager;
            EntityQuery
                entityQuery =
                    GetEntityQuery(
                        typeof(SpriteSheetData)); //TODO Replace spritesheetdata with our tag? or seperate after we have them all by our identifer tags
            NativeArray<Entity> entitiesToDraw =
                entityQuery.ToEntityArray(Allocator.TempJob);
            Entity
                e = entitiesToDraw[
                    0]; //Assume all entities share the shared data here (spritesheet cells, cell size, materials, etc.
            
            //Grab other shared data, 
            DynamicBuffer<AnimationClip> animationClips = entityManager.GetBuffer<AnimationClip>(e);
            MaterialPropertyBlock shaderVariables = new MaterialPropertyBlock();
            //
            //Per different Sprite
            //
            SpriteSheetData _spriteSheetData = entityManager.GetSharedComponentData<SpriteSheetData>(e); //Get one per different sprite
            Vector2 _spriteSheetUVs = _spriteSheetData.GridUVs;
            MeshData _meshData = entityManager.GetSharedComponentData<MeshData>(e);
            //
            // End per different sprite (e.g we can re-use these values for each entity with a tag (.eg goblin)
            //

            EntityQuery query = GetEntityQuery(typeof(UniqueAnimationData), typeof(Translation));
            NativeArray<UniqueAnimationData> uniqueAnimationDatas =
                query.ToComponentDataArray<UniqueAnimationData>(Allocator.TempJob);
            NativeArray<Translation> translationsToSort =
                query.ToComponentDataArray<Translation>(Allocator.TempJob);
            List<Matrix4x4> matrixList = new List<Matrix4x4>(uniqueAnimationDatas.Length);
            List<Vector4> uvList = new List<Vector4>(uniqueAnimationDatas.Length);

            //TODO make this sorting logic better, Split screen to slices and sort a slice on each thread
            
            for (int i = 0; i < translationsToSort.Length; i++) {
                for (int j = 0; j < translationsToSort.Length; j++)  {
                    if (translationsToSort[i].Value.y > translationsToSort[j].Value.y) {
                        
                        //swap
                        Translation tmpTranslation = translationsToSort[i];
                        translationsToSort[i] = translationsToSort[j];
                        translationsToSort[j] = tmpTranslation;

                        UniqueAnimationData tmpAnim = uniqueAnimationDatas[i];
                        uniqueAnimationDatas[i] = uniqueAnimationDatas[j];
                        uniqueAnimationDatas[j] = tmpAnim;
                    }
                }
            }
            

            
            for (int i = 0; i < uniqueAnimationDatas.Length; i++) {
                matrixList.Add(uniqueAnimationDatas[i].matrix);
                uvList.Add(new Vector4(_spriteSheetUVs.x, _spriteSheetUVs.y, uniqueAnimationDatas[i].offsetUVs.x,
                    uniqueAnimationDatas[i].offsetUVs.y));
            }

            int shaderPropertyId = Shader.PropertyToID("_MainTex_UV");
            shaderVariables.SetVectorArray(shaderPropertyId, uvList);
            Mesh mesh = MaterialStore.GetInstance().GetMeshById(_meshData.GetMeshID());
            
            Graphics.DrawMeshInstanced(
                mesh,
                0,
                _meshData.GetMaterial(),
                matrixList,
                shaderVariables
            );
            translationsToSort.Dispose();
            uniqueAnimationDatas.Dispose();
            entitiesToDraw.Dispose();
        }
    }
}