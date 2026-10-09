// ------------------------------------------------------------------------------------------------
//  <copyright file="ConstraintDtoBuilder.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tests.TestHelpers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using SysML2.NET.Core.Core.Types;
    using SysML2.NET.Core.DTO.Core.Features;
    using SysML2.NET.Core.DTO.Core.Types;
    using SysML2.NET.Core.DTO.Kernel.Behaviors;
    using SysML2.NET.Core.DTO.Kernel.Expressions;
    using SysML2.NET.Core.DTO.Kernel.FeatureValues;
    using SysML2.NET.Core.DTO.Kernel.Functions;
    using SysML2.NET.Core.DTO.Root.Elements;
    using SysML2.NET.Core.DTO.Root.Namespaces;
    using SysML2.NET.Core.DTO.Systems.Attributes;
    using SysML2.NET.Core.DTO.Systems.Constraints;
    using SysML2.NET.Core.DTO.Systems.DefinitionAndUsage;
    using SysML2.NET.Core.DTO.Systems.Enumerations;
    using SysML2.NET.Core.DTO.Systems.Requirements;
    using SysML2.NET.Core.Root.Namespaces;
    using SysML2.NET.Core.Systems.Requirements;

    /// <summary>
    /// Adds to the DTOs of a model subjects, attributes, enumerations and constraints of requirements with any expression
    /// (assumptions, comparisons between two attributes, numbers with units, logical operators, calls of functions), so
    /// that the tests can check how they are read and evaluated. The <see cref="ConstraintTextParser"/> builds the
    /// expressions from their textual notation.
    /// </summary>
    internal sealed class ConstraintDtoBuilder
    {
        /// <summary>
        /// The DTOs of the model, to which the built elements are added.
        /// </summary>
        private readonly List<IElement> dtos;

        /// <summary>
        /// The functions created for the calls, by name.
        /// </summary>
        private readonly Dictionary<string, Function> functionsByName = [];

        /// <summary>
        /// Initializes a new instance of the <see cref="ConstraintDtoBuilder"/> class.
        /// </summary>
        /// <param name="dtos">The DTOs of the model, to which the built elements are added.</param>
        public ConstraintDtoBuilder(List<IElement> dtos)
        {
            this.dtos = dtos;
        }

        /// <summary>
        /// Gets the subject of a requirement, as <c>subject subj;</c> declares it.
        /// </summary>
        /// <param name="requirementId">The <c>Id</c> of the requirement.</param>
        /// <returns>The subject.</returns>
        public IElement GetSubject(Guid requirementId)
        {
            var subjectId = this.dtos.OfType<SubjectMembership>().Single(membership => membership.OwningRelatedElement == requirementId).OwnedRelatedElement[0];

            return this.Get(subjectId);
        }

        /// <summary>
        /// Adds a subject named <c>subj</c> to a requirement, as <c>subject subj;</c> does.
        /// </summary>
        /// <param name="requirementId">The <c>Id</c> of the requirement.</param>
        /// <returns>The new subject.</returns>
        public ReferenceUsage Subject(Guid requirementId)
        {
            var subject = this.Add(new ReferenceUsage { DeclaredName = "subj", Direction = FeatureDirectionKind.In });
            this.AddOwnedRelationship(this.Get(requirementId), new SubjectMembership(), subject);

            return subject;
        }

        /// <summary>
        /// Adds an attribute, with or without value, as <c>attribute massLimit = 150;</c> does.
        /// </summary>
        /// <param name="ownerId">The <c>Id</c> of the owner of the attribute, for example a requirement or its subject.</param>
        /// <param name="name">The name of the attribute.</param>
        /// <param name="value">The expression of the value of the attribute, or <c>null</c>.</param>
        /// <returns>The new attribute.</returns>
        public AttributeUsage Attribute(Guid ownerId, string name, IElement value = null)
        {
            var attribute = this.Add(new AttributeUsage { DeclaredName = name });
            this.AddOwnedRelationship(this.Get(ownerId), new FeatureMembership(), attribute);

            if (value != null)
            {
                this.SetValue(attribute, value);
            }

            return attribute;
        }

        /// <summary>
        /// Gives a value to a feature.
        /// </summary>
        /// <param name="feature">The feature.</param>
        /// <param name="value">The expression of the value.</param>
        public void SetValue(IElement feature, IElement value)
        {
            this.AddOwnedRelationship(feature, new FeatureValue(), value);
        }

        /// <summary>
        /// Adds a constraint to a requirement, as <c>require constraint { ... }</c> or <c>assume constraint { ... }</c> does.
        /// </summary>
        /// <param name="requirementId">The <c>Id</c> of the requirement.</param>
        /// <param name="kind">Whether the constraint is required or assumed.</param>
        /// <param name="expression">The expression of the constraint, or <c>null</c> for a constraint without expression.</param>
        /// <returns>The new constraint.</returns>
        public ConstraintUsage Constraint(Guid requirementId, RequirementConstraintKind kind, IElement expression)
        {
            var constraint = this.Add(new ConstraintUsage { IsComposite = true });
            this.AddOwnedRelationship(this.Get(requirementId), new RequirementConstraintMembership { Kind = kind }, constraint);

            if (expression != null)
            {
                this.AddOwnedRelationship(constraint, new ResultExpressionMembership(), expression);
            }

            return constraint;
        }

        /// <summary>
        /// Creates a literal number with decimals.
        /// </summary>
        /// <param name="value">The value of the literal.</param>
        /// <returns>The new <see cref="LiteralRational"/>.</returns>
        public LiteralRational Literal(double value)
        {
            return this.Add(new LiteralRational { Value = value });
        }

        /// <summary>
        /// Creates a literal integer.
        /// </summary>
        /// <param name="value">The value of the literal.</param>
        /// <returns>The new <see cref="LiteralInteger"/>.</returns>
        public LiteralInteger Integer(int value)
        {
            return this.Add(new LiteralInteger { Value = value });
        }

        /// <summary>
        /// Creates a literal Boolean.
        /// </summary>
        /// <param name="value">The value of the literal.</param>
        /// <returns>The new <see cref="LiteralBoolean"/>.</returns>
        public LiteralBoolean Boolean(bool value)
        {
            return this.Add(new LiteralBoolean { Value = value });
        }

        /// <summary>
        /// Creates a literal text.
        /// </summary>
        /// <param name="value">The value of the literal.</param>
        /// <returns>The new <see cref="LiteralString"/>.</returns>
        public LiteralString Text(string value)
        {
            return this.Add(new LiteralString { Value = value });
        }

        /// <summary>
        /// Creates the expression <c>null</c>.
        /// </summary>
        /// <returns>The new <see cref="NullExpression"/>.</returns>
        public NullExpression Null()
        {
            return this.Add(new NullExpression());
        }

        /// <summary>
        /// Creates the literal infinity, <c>*</c>.
        /// </summary>
        /// <returns>The new <see cref="LiteralInfinity"/>.</returns>
        public LiteralInfinity Infinity()
        {
            return this.Add(new LiteralInfinity());
        }

        /// <summary>
        /// Creates a number with a unit, such as <c>150 [kg]</c>, whose unit is referenced by its name, as when the SI
        /// library is not in the model.
        /// </summary>
        /// <param name="value">The expression of the number.</param>
        /// <param name="unit">The name of the unit, or an expression of units.</param>
        /// <returns>The new <see cref="OperatorExpression"/>.</returns>
        public OperatorExpression Quantity(IElement value, IElement unit)
        {
            return this.Operation("[", value, unit);
        }

        /// <summary>
        /// Creates a reference to an element that is not in the model, such as a unit of the SI library, by its name.
        /// </summary>
        /// <param name="name">The name of the referenced element, for example <c>kg</c>.</param>
        /// <returns>The new <see cref="FeatureReferenceExpression"/>.</returns>
        public FeatureReferenceExpression NamedReference(string name)
        {
            var reference = this.Add(new FeatureReferenceExpression());
            this.AddOwnedRelationship(reference, new Membership { MemberElement = Guid.NewGuid(), MemberName = name, MemberShortName = name });

            return reference;
        }

        /// <summary>
        /// Creates a feature chain whose target is designated by its name only, such as <c>.mass</c> in <c>subj.mass</c>.
        /// </summary>
        /// <param name="source">The expression of the source of the chain, or <c>null</c> for a chain without source.</param>
        /// <param name="targetName">The name of the target feature.</param>
        /// <returns>The new <see cref="FeatureChainExpression"/>.</returns>
        public FeatureChainExpression NamedChain(IElement source, string targetName)
        {
            var chain = this.Add(new FeatureChainExpression { Operator = "." });

            if (source != null)
            {
                this.AddArgument(chain, source);
            }

            this.AddOwnedRelationship(chain, new Membership { MemberElement = Guid.NewGuid(), MemberName = targetName });

            return chain;
        }

        /// <summary>
        /// Creates a call of a function, such as <c>max(a, b)</c>: an <see cref="InvocationExpression"/> typed by a function of
        /// that name, created once.
        /// </summary>
        /// <param name="functionName">The name of the function, or <c>null</c> for a call of a function that is not in the model.</param>
        /// <param name="arguments">The arguments.</param>
        /// <returns>The new <see cref="InvocationExpression"/>.</returns>
        public InvocationExpression Invocation(string functionName, params IElement[] arguments)
        {
            var invocation = this.Add(new InvocationExpression());

            if (functionName != null)
            {
                if (!this.functionsByName.TryGetValue(functionName, out var function))
                {
                    function = this.Add(new Function { DeclaredName = functionName });
                    this.functionsByName[functionName] = function;
                }

                this.AddOwnedRelationship(invocation, new FeatureTyping { Type = function.Id, TypedFeature = invocation.Id });
            }

            foreach (var argument in arguments)
            {
                this.AddArgument(invocation, argument);
            }

            return invocation;
        }

        /// <summary>
        /// Adds an enumeration definition and its values, as <c>enum def OrbitKind { enum sunSynchronous; enum polar; }</c> does.
        /// </summary>
        /// <param name="ownerId">The <c>Id</c> of the owner of the definition, for example a package.</param>
        /// <param name="name">The name of the enumeration definition.</param>
        /// <param name="valueNames">The names of its values.</param>
        /// <returns>The values, in order.</returns>
        public IReadOnlyList<EnumerationUsage> Enumeration(Guid ownerId, string name, params string[] valueNames)
        {
            var definition = this.Add(new EnumerationDefinition { DeclaredName = name });
            this.AddOwnedRelationship(this.Get(ownerId), new OwningMembership(), definition);

            return valueNames.Select(valueName =>
            {
                var enumerationValue = this.Add(new EnumerationUsage { DeclaredName = valueName });
                this.AddOwnedRelationship(definition, new VariantMembership(), enumerationValue);

                return enumerationValue;
            }).ToList();
        }

        /// <summary>
        /// Creates a feature that chains other features, as the target <c>camera.mass</c> of <c>subj.camera.mass</c> can be.
        /// </summary>
        /// <param name="chainingFeatureIds">The <c>Id</c>s of the chained features, in order.</param>
        /// <returns>The new <see cref="Feature"/>.</returns>
        public Feature ChainedFeature(params Guid[] chainingFeatureIds)
        {
            var feature = this.Add(new Feature());

            foreach (var chainingFeatureId in chainingFeatureIds)
            {
                this.AddOwnedRelationship(feature, new FeatureChaining { ChainingFeature = chainingFeatureId });
            }

            return feature;
        }

        /// <summary>
        /// Creates a reference to a feature, such as <c>massLimit</c>.
        /// </summary>
        /// <param name="featureId">The <c>Id</c> of the referenced feature.</param>
        /// <returns>The new <see cref="FeatureReferenceExpression"/>.</returns>
        public FeatureReferenceExpression Reference(Guid featureId)
        {
            var reference = this.Add(new FeatureReferenceExpression());
            this.AddOwnedRelationship(reference, new Membership { MemberElement = featureId });

            return reference;
        }

        /// <summary>
        /// Creates a feature chain, such as <c>subj.mass</c>.
        /// </summary>
        /// <param name="source">The expression of the source of the chain, for example a reference to the subject.</param>
        /// <param name="targetId">The <c>Id</c> of the target feature, for example the attribute <c>mass</c>.</param>
        /// <returns>The new <see cref="FeatureChainExpression"/>.</returns>
        public FeatureChainExpression Chain(IElement source, Guid targetId)
        {
            var chain = this.Add(new FeatureChainExpression { Operator = "." });
            this.AddArgument(chain, source);
            this.AddOwnedRelationship(chain, new Membership { MemberElement = targetId });

            return chain;
        }

        /// <summary>
        /// Creates an operation, such as <c>subj.mass * 1.2</c> or <c>subj.mass &lt;= 150</c>.
        /// </summary>
        /// <param name="operatorSymbol">The operator.</param>
        /// <param name="arguments">The arguments, with <c>null</c> for a parameter without value.</param>
        /// <returns>The new <see cref="OperatorExpression"/>.</returns>
        public OperatorExpression Operation(string operatorSymbol, params IElement[] arguments)
        {
            var operation = this.Add(new OperatorExpression { Operator = operatorSymbol });

            foreach (var argument in arguments)
            {
                this.AddArgument(operation, argument);
            }

            return operation;
        }

        /// <summary>
        /// Adds an argument to an expression: an input parameter whose value is the argument.
        /// </summary>
        /// <param name="expression">The expression.</param>
        /// <param name="argument">The argument, or <c>null</c> for a parameter without value.</param>
        private void AddArgument(IElement expression, IElement argument)
        {
            var parameter = this.Add(new Feature { Direction = FeatureDirectionKind.In });
            this.AddOwnedRelationship(expression, new ParameterMembership(), parameter);

            if (argument != null)
            {
                this.AddOwnedRelationship(parameter, new FeatureValue(), argument);
            }
        }

        /// <summary>
        /// Adds a relationship owned by an element, and the element that the relationship owns, if any.
        /// </summary>
        /// <param name="owner">The owner of the relationship.</param>
        /// <param name="relationship">The relationship.</param>
        /// <param name="ownedRelatedElement">The element owned by the relationship, or <c>null</c>.</param>
        private void AddOwnedRelationship(IElement owner, IRelationship relationship, IElement ownedRelatedElement = null)
        {
            if (relationship is IMembership membership)
            {
                membership.Visibility = VisibilityKind.Public;
            }

            this.Add(relationship);
            relationship.OwningRelatedElement = owner.Id;
            owner.OwnedRelationship.Add(relationship.Id);

            if (ownedRelatedElement != null)
            {
                relationship.OwnedRelatedElement.Add(ownedRelatedElement.Id);
                ownedRelatedElement.OwningRelationship = relationship.Id;
            }
        }

        /// <summary>
        /// Adds an element to the model, with a new identifier.
        /// </summary>
        /// <typeparam name="T">The type of the element.</typeparam>
        /// <param name="element">The element.</param>
        /// <returns>The element.</returns>
        private T Add<T>(T element) where T : IElement
        {
            element.Id = Guid.NewGuid();
            element.ElementId = element.Id.ToString();
            this.dtos.Add(element);

            return element;
        }

        /// <summary>
        /// Gets an element of the model.
        /// </summary>
        /// <param name="id">The <c>Id</c> of the element.</param>
        /// <returns>The element.</returns>
        private IElement Get(Guid id)
        {
            return this.dtos.Single(dto => dto.Id == id);
        }
    }
}
