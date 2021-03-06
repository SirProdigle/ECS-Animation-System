﻿using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Prutils.SpriteAnimation
{

    public enum AnimationDirection
    {
        Up,
        Down,
        Left,
        Right
    }

//Holds the start/end cell/direction & speed of an individual animation from a spritesheet
    public struct AnimationClip : IBufferElementData
    {
        public AnimationDirection Direction;
        public int StartCell;  //Cells are counted top-left = 0 and go right, wrapping to next row
        public int TotalCells;
        public float FrameTimerMax;

        
        public AnimationClip(AnimationDirection direction, int startCell, int totalCells, float frameTimerMax) {
            Direction = direction;
            StartCell = startCell;
            TotalCells = totalCells;
            FrameTimerMax = frameTimerMax;
        }

    }


//Holds the data of a spritesheet, e.g how many cells, list of animations and their cells, and a frametimer counter
/*
 * TODO This should NOT be a componentData. Perhaps a BlobAssetReference or dictionary of int to SSD. This is READONLYDATA that does NOT change. It is simply the readonly data
 * for each spritesheet (num cells, per row, UV's etc. So for all characters that use the hero spritesheet, this is all the same. Originally had this as SharedData, but that
 * makes no sense if it does not change. Discord told me to look into BlobAssetReference or a Dictionary/Array that entities stare an int id for. for now though it works with
 * burst
 */
    public struct SpriteSheetData : IComponentData
    {
        public int TotalCells;
        public int CellsPerRow;
        public Vector2 GridUVs;

    }

    public struct UniqueAnimationData : IComponentData
    {
        public float FrameTimer;
        public Vector2 offsetUVs;
        public AnimationClip currentClip;
        public int currentFrame;
        public Matrix4x4 matrix; //Do not touch, function to calculate for rendering
    }

    public class AnimationSystem : JobComponentSystem
    {
       
        [BurstCompile]
        public new struct Job : IJobForEachWithEntity<UniqueAnimationData,Translation, SpriteSheetData>
        {
            public float deltaTime;

            [BurstCompile]
            public void Execute(Entity e, int index, ref UniqueAnimationData uniqueAnimationData, ref Translation translation, ref SpriteSheetData ssd) {
                SpriteSheetData spriteSheetData = ssd;
                AnimationClip currentAnimation = uniqueAnimationData.currentClip; //Grab our current animation
                
                
                //
                //Calculate current frame
                //
                uniqueAnimationData.FrameTimer += deltaTime;
                if (uniqueAnimationData.FrameTimer > currentAnimation.FrameTimerMax) {
                    uniqueAnimationData.FrameTimer -= currentAnimation.FrameTimerMax;
                    uniqueAnimationData.currentFrame =
                        (uniqueAnimationData.currentFrame + 1) % currentAnimation.TotalCells;
                }



                float uvHeight = spriteSheetData.GridUVs.x;
                float uvWidth = spriteSheetData.GridUVs.y;
                //These will depend on current animation clip, need to be calculated
                Vector2 startingClipOffset = new Vector2();
                startingClipOffset.x = (currentAnimation.StartCell % spriteSheetData.CellsPerRow) * uvWidth;
                startingClipOffset.y =  0.75f - (math.floor((float)currentAnimation.StartCell / (float)spriteSheetData.CellsPerRow) * uvHeight);
                switch (currentAnimation.Direction) {
                    case AnimationDirection.Right:
                        startingClipOffset.x += uniqueAnimationData.currentFrame * uvWidth;
                        break;
                    case AnimationDirection.Down:
                        startingClipOffset.y -= uniqueAnimationData.currentFrame * uvHeight;
                        break;
                    case AnimationDirection.Up:
                        startingClipOffset.y += uniqueAnimationData.currentFrame * uvHeight;
                        break;
                }
                uniqueAnimationData.offsetUVs = new Vector2( startingClipOffset.x, startingClipOffset.y);
                float3 position = translation.Value;
                position.z = position.y * 0.1f; //sprite sorting
                //translation.Value = position; //TODO check if we really need this line?
                uniqueAnimationData.matrix = Matrix4x4.TRS(position, Quaternion.identity, Vector3.one);
            }

        }

        protected override JobHandle OnUpdate(JobHandle inputDeps) {
            BufferFromEntity<AnimationClip> animationClips = GetBufferFromEntity<AnimationClip>();
            Job job = new Job() {
                    deltaTime = Time.DeltaTime,
            };
                return job.Schedule(this, inputDeps);
            }
        
    }
}
    



