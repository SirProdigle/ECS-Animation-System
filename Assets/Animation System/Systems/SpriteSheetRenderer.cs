using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Prutils.SpriteAnimation
{

    [UpdateAfter(typeof(AnimationSystem))]
    public class SpriteSheetRenderer : ComponentSystem
    {

        struct RenderData
        {
            public Entity e;
            public float3 position;
            public Matrix4x4 matrix;
            public Vector4 uv;
        }

        [BurstCompile]
        private struct CullAndSortJob : IJobForEachWithEntity<Translation, UniqueAnimationData>
        {
            public float YTop1;
            public float YTop2;
            public float YBottom;
        
            public NativeQueue<RenderData>.ParallelWriter nativeQueue_1;
            public NativeQueue<RenderData>.ParallelWriter nativeQueue_2;
            public Vector2 spriteSheetUVs;
            
            public void Execute(Entity entity, int index, ref Translation translation, ref UniqueAnimationData uAnimData) {
                float positionY = translation.Value.y;
                if (positionY > YBottom && positionY < YTop1) {
                    //Valid pos
                    RenderData renderData = new RenderData() {
                        e = entity,
                        position = translation.Value,
                        matrix = uAnimData.matrix,
                        uv = new Vector4(spriteSheetUVs.x,spriteSheetUVs.y,uAnimData.offsetUVs.x,uAnimData.offsetUVs.y)
                    };
                    if (positionY < YTop2) {
                        nativeQueue_2.Enqueue(renderData);
                    }
                    else {
                        nativeQueue_1.Enqueue(renderData);
                    }
                    
                }
            }
        }
        [BurstCompile]
        private struct RenderQueueToArrayJob : IJob
        {
            public NativeQueue<RenderData> renderQueue1;
            public NativeArray<RenderData> renderArray;
            public void Execute() {
                int index = 0;
                RenderData renderData;
                while (renderQueue1.TryDequeue(out renderData)) {
                    renderArray[index] = renderData;
                    index++;
                }
            }
        }
        [BurstCompile]
        private struct SortByPositionJob : IJob
        {
            public NativeArray<RenderData> sortArray;
            public void Execute() {
                for (int i = 0; i < sortArray.Length; i++) {
                    for (int j = 0; j < sortArray.Length; j++)  {
                        if (sortArray[i].position.y > sortArray[j].position.y) {
                        
                            //swap
                            RenderData tmpRenderData = sortArray[i];
                            sortArray[i] = sortArray[j];
                            sortArray[j] = tmpRenderData;
                        }
                    }
                }
            }
        }
        [BurstCompile]
        private struct FillArraysParralelJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<RenderData> nativeArray;
            [NativeDisableContainerSafetyRestriction]public NativeArray<Matrix4x4> matrixArray;
            [NativeDisableContainerSafetyRestriction]public NativeArray<Vector4> uvArray;
            public int startingIndex;
            public void Execute(int index) {
                RenderData renderData = nativeArray[index];
                matrixArray[startingIndex + index] = renderData.matrix;
                uvArray[startingIndex + index] = renderData.uv;
            }
        }

        //
        //
        //
        //END OF SPLIT MULTITHREADED CODE
        ///
        //
        //
        //
        //
        //
        
        //TODO Basically re-do this code once per different sprite tag, e.g once for User and once for Goblins, then we do 1 draw call per spritesheet
        protected override void OnUpdate() {
            //Get our entity reference
            EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
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
            SpriteSheetData _spriteSheetData = entityManager.GetComponentData<SpriteSheetData>(e); //Get one per different sprite
            Vector2 _spriteSheetUVs = _spriteSheetData.GridUVs;
            MeshData _meshData = entityManager.GetSharedComponentData<MeshData>(e);
            //
            // End per different sprite (e.g we can re-use these values for each entity with a tag (.eg goblin)
            //

            EntityQuery query = GetEntityQuery(typeof(UniqueAnimationData), typeof(Translation));
            NativeArray<UniqueAnimationData> uniqueAnimationDatas =
                query.ToComponentDataArray<UniqueAnimationData>(Allocator.TempJob);


            NativeQueue<RenderData> renderQueue1 = new NativeQueue<RenderData>(Allocator.TempJob);
            NativeQueue<RenderData> renderQueue2 = new NativeQueue<RenderData>(Allocator.TempJob);
            Camera camera = Camera.main;
            float3 cameraPos = camera.transform.position;
            float yBottom = cameraPos.y - camera.orthographicSize;
            float yTop1 = cameraPos.y + camera.orthographicSize;
            float yTop2 = cameraPos.y  + 0f;

            CullAndSortJob cullAndSortJob = new CullAndSortJob() {
                YBottom =  yBottom,
                YTop1 = yTop1,
                YTop2 = yTop2,
                nativeQueue_1 = renderQueue1.AsParallelWriter(),
                nativeQueue_2 = renderQueue2.AsParallelWriter(),
                spriteSheetUVs = _spriteSheetData.GridUVs
                
            };
            JobHandle jobHandle = cullAndSortJob.Schedule(this);
            jobHandle.Complete();

            NativeArray<JobHandle> jobHandles = new NativeArray<JobHandle>(2,Allocator.TempJob);
            NativeArray<RenderData> renderArray1 = new NativeArray<RenderData>(renderQueue1.Count,Allocator.TempJob);
            NativeArray<RenderData> renderArray2 = new NativeArray<RenderData>(renderQueue2.Count,Allocator.TempJob);
            
            RenderQueueToArrayJob renderQueueToArrayJob1 = new RenderQueueToArrayJob() {
                renderQueue1 = renderQueue1,
                renderArray = renderArray1
            };
            RenderQueueToArrayJob renderQueueToArrayJob2 = new RenderQueueToArrayJob() {
                renderQueue1 = renderQueue2,
                renderArray = renderArray2
            };
            jobHandles[0] = renderQueueToArrayJob1.Schedule();
            jobHandles[1] = renderQueueToArrayJob2.Schedule();
            JobHandle.CompleteAll(jobHandles);
            renderQueue1.Dispose();
            renderQueue2.Dispose();

            SortByPositionJob sortByPositionJob1 = new SortByPositionJob() {
                sortArray = renderArray1
            };
            SortByPositionJob sortByPositionJob2 = new SortByPositionJob() {
                sortArray = renderArray2
            };
            jobHandles[0] = sortByPositionJob1.Schedule();
            jobHandles[1] = sortByPositionJob2.Schedule();
            JobHandle.CompleteAll(jobHandles);

            int visibleEntityTotal = renderArray1.Length + renderArray2.Length;
            NativeArray<Matrix4x4> matrixArray = new NativeArray<Matrix4x4>(visibleEntityTotal,Allocator.TempJob);
            NativeArray<Vector4> uvArray = new NativeArray<Vector4>(visibleEntityTotal,Allocator.TempJob);

            FillArraysParralelJob fillArraysParralelJob1 = new FillArraysParralelJob() {
                nativeArray = renderArray1,
                matrixArray = matrixArray,
                uvArray = uvArray,
                startingIndex = 0
            };
            jobHandles[0] = fillArraysParralelJob1.Schedule(renderArray1.Length,10);
            FillArraysParralelJob fillArraysParralelJob2 = new FillArraysParralelJob() {
                nativeArray = renderArray2,
                matrixArray = matrixArray,
                uvArray = uvArray,
                startingIndex = renderArray1.Length
            };
            jobHandles[1] = fillArraysParralelJob2.Schedule(renderArray2.Length,10);
            
            JobHandle.CompleteAll(jobHandles);
            

            int shaderPropertyId = Shader.PropertyToID("_MainTex_UV");
            Mesh mesh = MaterialStore.GetInstance().GetMeshById(_meshData.GetMeshID());

            int sliceCount = 1023;
            Matrix4x4[] matrixInstancedArray = new Matrix4x4[sliceCount];
            Vector4[] uvInstancedArray = new Vector4[sliceCount];
            for (int i = 0; i < uniqueAnimationDatas.Length; i+=sliceCount) {
                int sliceSize = math.min(uniqueAnimationDatas.Length - i, sliceCount);
                
                NativeArray<Matrix4x4>.Copy(matrixArray,i,matrixInstancedArray,0,sliceSize);
                NativeArray<Vector4>.Copy(uvArray,i,uvInstancedArray,0,sliceSize);
                
                shaderVariables.SetVectorArray(shaderPropertyId, uvInstancedArray);
                
                Graphics.DrawMeshInstanced(
                    mesh,
                    0,
                    _meshData.GetMaterial(),
                    matrixInstancedArray,
                    sliceSize,
                    shaderVariables
                );
            }
            
            matrixArray.Dispose();
            uvArray.Dispose();
            uniqueAnimationDatas.Dispose();
            entitiesToDraw.Dispose();
            jobHandles.Dispose();
            renderArray1.Dispose();
            renderArray2.Dispose();
        }
    }
}