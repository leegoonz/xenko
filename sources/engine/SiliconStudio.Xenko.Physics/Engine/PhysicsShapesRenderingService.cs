// Copyright (c) 2014-2016 Silicon Studio Corp. (http://siliconstudio.co.jp)
// This file is distributed under GPL v3. See LICENSE.md for details.

using System;
using System.Collections.Generic;
using SiliconStudio.Core;
using SiliconStudio.Core.Mathematics;
using SiliconStudio.Xenko.Engine;
using SiliconStudio.Xenko.Graphics;
using SiliconStudio.Xenko.Rendering;

namespace SiliconStudio.Xenko.Physics.Engine
{
    public class PhysicsShapesRenderingService : GameSystem
    {
        private Material triggerMaterial;
        private Material staticMaterial;
        private Material dynamicMaterial;
        private Material kinematicMaterial;
        private Material characterMaterial;
        private GraphicsDevice graphicsDevice;

        private Dictionary<Type, MeshDraw> debugMeshCache = new Dictionary<Type, MeshDraw>(); 

        public override void Initialize()
        {
            graphicsDevice = Services.GetServiceAs<IGraphicsDeviceService>().GraphicsDevice;

            triggerMaterial = PhysicsDebugShapeMaterial.Create(graphicsDevice, Color.AdjustSaturation(Color.Purple, 0.77f), 1);
            staticMaterial = PhysicsDebugShapeMaterial.Create(graphicsDevice, Color.AdjustSaturation(Color.Red, 0.77f), 1);
            dynamicMaterial = PhysicsDebugShapeMaterial.Create(graphicsDevice, Color.AdjustSaturation(Color.Green, 0.77f), 1);
            kinematicMaterial = PhysicsDebugShapeMaterial.Create(graphicsDevice, Color.AdjustSaturation(Color.Blue, 0.77f), 1);
            characterMaterial = PhysicsDebugShapeMaterial.Create(graphicsDevice, Color.AdjustSaturation(Color.Yellow, 0.77f), 1);
        }

        public PhysicsShapesRenderingService(IServiceRegistry registry) : base(registry)
        {            
        }

        public Entity CreateDebugEntity(PhysicsComponent component)
        {
            if (component?.ColliderShape == null) return null;

            if (component.DebugEntity != null) return null;

            var debugEntity = new Entity();

            var colliderEntity = CreateChildEntity(component, component.ColliderShape, true);
            if (colliderEntity == null) return null;

            debugEntity.AddChild(colliderEntity);

            if (component.CanScaleShape)
            {
                debugEntity.Transform.Scale = component.ColliderShape.Scaling;
            }

            var skinnedElement = component as PhysicsSkinnedComponentBase;
            if (skinnedElement != null && skinnedElement.BoneIndex != -1)
            {
                Vector3 scale, pos;
                Quaternion rot;
                skinnedElement.BoneWorldMatrixOut.Decompose(out scale, out rot, out pos);
                debugEntity.Transform.Position = pos;
                debugEntity.Transform.Rotation = rot;
            }
            else
            {
                Vector3 scale, pos;
                Quaternion rot;
                component.Entity.Transform.WorldMatrix.Decompose(out scale, out rot, out pos);
                debugEntity.Transform.Position = pos;
                debugEntity.Transform.Rotation = rot;
            }

            return debugEntity;
        }

        private Entity CreateChildEntity(PhysicsComponent component, ColliderShape shape, bool addOffset = false)
        {
            if (shape  == null)
                return null;

            switch (shape.Type)
            {
                case ColliderShapeTypes.StaticPlane:
                    {
                        //Hmm TODO maybe can draw an infinite plane??
                        return null;
                    }
                case ColliderShapeTypes.Compound:
                    {
                        var entity = new Entity();

                        //We got to recurse
                        var compound = (CompoundColliderShape)shape;
                        for (var i = 0; i < compound.Count; i++)
                        {
                            var subShape = compound[i];
                            var subEntity = CreateChildEntity(component, subShape, true);

                            subEntity.Transform.UseTRS = false;
                            entity.AddChild(subEntity);
                        }

                        entity.Transform.LocalMatrix = Matrix.Identity;
                        entity.Transform.UseTRS = false;

                        return entity;
                    }
                default:
                    {
                        var mat = triggerMaterial;

                        var rigidbodyComponent = component as RigidbodyComponent;
                        if (rigidbodyComponent != null)
                        {
                            mat = rigidbodyComponent.IsKinematic ? kinematicMaterial : dynamicMaterial;
                        }

                        else if (component is CharacterComponent)
                        {
                            mat = characterMaterial;
                        }
                        else if (component is StaticColliderComponent)
                        {
                            mat = staticMaterial;
                        }

                        MeshDraw draw;
                        if (!debugMeshCache.TryGetValue(shape.GetType(), out draw))
                        {
                            draw = shape.CreateDebugPrimitive(graphicsDevice);
                            debugMeshCache[shape.GetType()] = draw;
                        }

                        var entity = new Entity
                        {
                            new ModelComponent
                            {
                                Model = new Model
                                {
                                    mat,
                                    new Mesh
                                    {
                                        Draw = draw
                                    }
                                }
                            }
                        };

                        var offset = addOffset ? Matrix.RotationQuaternion(shape.LocalRotation)*Matrix.Translation(shape.LocalOffset) : Matrix.Identity;

                        if (shape.Type == ColliderShapeTypes.ConvexHull)
                        {
                            var hullDesc = (ConvexHullColliderShape)shape;

                            entity.Transform.LocalMatrix = shape.DebugPrimitiveMatrix * Matrix.Scaling(hullDesc.Scaling) * offset;
                        }
                        else
                        {
                            entity.Transform.LocalMatrix = shape.DebugPrimitiveMatrix * offset;
                        }
                        entity.Transform.UseTRS = false;

                        return entity;
                    }
            }
        }
    }
}