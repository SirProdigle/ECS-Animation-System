using System;
using System.Collections;
using System.Collections.Generic;
using Prutils.SpriteAnimation;
using UnityEngine;
using Unity.Entities;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using AnimationClip = Prutils.SpriteAnimation.AnimationClip;
using Random = UnityEngine.Random;

public class Testing : MonoBehaviour
{


    // Start is called before the first frame update
    private void Awake() {
        EntityManager manager = World.DefaultGameObjectInjectionWorld.EntityManager;
        EntityArchetype archetype = manager.CreateArchetype(
            typeof(Translation),
            typeof(UniqueAnimationData),
            typeof(AnimationClip),
            typeof(SpriteSheetData),
            typeof(MeshData)
        );
        NativeArray<Entity> entities = new NativeArray<Entity>(10000, Allocator.Temp);
        manager.CreateEntity(archetype, entities);
        MeshData mesh = new MeshData(1, 1, 0);
        foreach (Entity e in entities) {


            manager.SetComponentData(e,
                new Translation() {
                    Value = new float3(Random.Range(-11f,10.5f), Random.Range(-5f,4.5f), 0)
                });
            
            manager.SetSharedComponentData(e, mesh);
                


            //
            //Calculate uvs
            //
            //Width and height will be static per sheet, TODO cache these


            SpriteSheetData spriteSheetData = new SpriteSheetData() {
                TotalCells = 16,
                CellsPerRow = 4,
                GridUVs = new Vector2()
            };
            //
            //Calculate uvs
            //
            //Width and height will be static per sheet, TODO cache these
            float uvWidth = 1f / spriteSheetData.CellsPerRow;
            float uvHeight = 1 / math.ceil(((float) spriteSheetData.TotalCells / (float) spriteSheetData.CellsPerRow));
            spriteSheetData.GridUVs.x = uvWidth;
            spriteSheetData.GridUVs.y = uvHeight;
            manager.SetComponentData(e,
                new SpriteSheetData() {
                    TotalCells = spriteSheetData.TotalCells,
                    CellsPerRow = spriteSheetData.CellsPerRow,
                    GridUVs = spriteSheetData.GridUVs
                });

            var animationClips =
                manager.AddBuffer<AnimationClip>(
                    e); //TODO make this a shared buffer between all meeple entities if possible??
            AnimationClip walkDown = new AnimationClip(AnimationDirection.Right, 0, 4, .25f);
            AnimationClip walkLeft = new AnimationClip(AnimationDirection.Right, 4, 4, 1);
            animationClips.Add(walkDown);
            animationClips.Add(walkLeft);

            manager.SetComponentData(e,
                new UniqueAnimationData() {
                    currentClip = Random.Range(0,2) == 0 ? walkDown : walkLeft,
                    currentFrame = Random.Range(0,4),
                    FrameTimer = Random.Range(0f,1f),
                    offsetUVs = new Vector2()
                });

        }

        entities.Dispose();
        
    }
}
