// ------------------------------------------------------------------------------------------------
//  <copyright file="ModelChangeBatch.Behaviour.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Changes
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using SysML2.NET.Core.Core.Types;
    using SysML2.NET.Core.DTO.Core.Classifiers;
    using SysML2.NET.Core.DTO.Core.Features;
    using SysML2.NET.Core.DTO.Core.Types;
    using SysML2.NET.Core.DTO.Kernel.Behaviors;
    using SysML2.NET.Core.DTO.Kernel.Connectors;
    using SysML2.NET.Core.DTO.Kernel.Interactions;
    using SysML2.NET.Core.DTO.Root.Elements;
    using SysML2.NET.Core.DTO.Root.Namespaces;
    using SysML2.NET.Core.DTO.Systems.Actions;
    using SysML2.NET.Core.DTO.Systems.DefinitionAndUsage;
    using SysML2.NET.Core.DTO.Systems.Flows;
    using SysML2.NET.Core.DTO.Systems.Ports;
    using SysML2.NET.Core.DTO.Systems.States;
    using SysML2.NET.Core.Root.Namespaces;
    using SysML2.NET.Core.Systems.States;

    /// <content>
    /// The behaviour: the transitions between states or actions, which build, as the textual notation does,
    /// <c>transition first launch accept LaunchCompleted if batteryCharge &gt;= 80 do deployPanels then cruise;</c>, the entry,
    /// do and exit actions of a state (<c>entry action initialise;</c>, <c>exit applyBrake;</c>), and the ends and the item
    /// of a flow (<c>flow of Image from camera.dataOut to obc.dataIn;</c>).
    /// </content>
    internal sealed partial class ModelChangeBatch
    {
        /// <summary>
        /// The role, in the messages, of the definition of the event that triggers a transition.
        /// </summary>
        private const string TriggerRole = "trigger";

        /// <summary>
        /// The role, in the messages, of the action that a transition performs.
        /// </summary>
        private const string EffectRole = "effect";

        /// <summary>
        /// The role, in the messages, of the definition of what flows through a flow.
        /// </summary>
        private const string ItemRole = "item";

        /// <summary>
        /// Refuses the properties of a payload that only apply to the creation of a transition, of an action of a state or
        /// of a flow, when another element is created.
        /// </summary>
        /// <param name="element">The created element.</param>
        /// <param name="payload">The payload of the change.</param>
        /// <exception cref="InvalidChangeException">Thrown when a property does not apply to the element.</exception>
        private static void CheckBehaviourProperties(IElement element, ElementPayload payload)
        {
            if (payload.Trigger != null || payload.Guard != null || payload.Effect != null)
            {
                throw new InvalidChangeException($"Only a TransitionUsage has a trigger, a guard or an effect, not a {element.GetType().Name}.");
            }

            if (payload.Item != null && element is not IFlowUsage)
            {
                throw new InvalidChangeException($"Only a FlowUsage has an item, not a {element.GetType().Name}.");
            }
        }

        /// <summary>
        /// Refuses the properties of a payload that only apply to the creation of a transition, of an action of a state or
        /// of a flow, when an element is updated.
        /// </summary>
        /// <param name="element">The updated element.</param>
        /// <param name="payload">The payload of the change.</param>
        /// <exception cref="InvalidChangeException">Thrown when the payload has one of these properties.</exception>
        private static void CheckNoBehaviourUpdate(IElement element, ElementPayload payload)
        {
            if (payload is not { Trigger: null, Guard: null, Effect: null, StateAction: null, Item: null })
            {
                throw new InvalidChangeException($"trigger, guard, effect, stateAction and item cannot change: delete the {Describe(element)} and create it again.");
            }
        }

        /// <summary>
        /// Creates a transition between two states or two actions, as
        /// <c>transition first launch accept LaunchCompleted if batteryCharge &gt;= 80 do deployPanels then cruise;</c> does:
        /// a <c>TransitionUsage</c> whose first membership designates its source, with an empty input parameter, an
        /// <c>AcceptActionUsage</c> for its trigger (with a second empty input parameter), an expression for its guard, a
        /// <c>PerformActionUsage</c> for its effect, and a <c>Succession</c> whose second end designates its target. The first
        /// end of the succession, empty in the textual notation, references the source, as the constraint
        /// <c>checkTransitionUsageSuccessionSourceSpecialization</c> requires: the derived <c>target</c> of the transition
        /// is read from it.
        /// </summary>
        /// <param name="transition">The new <c>TransitionUsage</c>, which has no identifier yet.</param>
        /// <param name="change">The change that creates it, whose identity is an optional temporary name.</param>
        /// <exception cref="InvalidChangeException">Thrown when the change cannot be applied.</exception>
        private void CreateTransition(IElement transition, ModelChange change)
        {
            var payload = change.Payload;

            if (payload is not { Definition: null, Value: null, Unit: null, Constraint: null, SatisfiedRequirement: null, SatisfyingPart: null, Direction: null, Conjugated: null, IsEnd: null, Specializes: null, Performed: null, Item: null }
                || !string.IsNullOrWhiteSpace(payload.ReqId))
            {
                throw new InvalidChangeException("A TransitionUsage takes an owner and two ends, and optionally a name, a trigger, a guard, an effect and a text.");
            }

            var owner = this.Resolve(payload.Owner, OwnerRole);
            const string expectedOwner = "a state or an action, for example the StateDefinition or the StateUsage that holds the states it links";
            CheckKind(owner, owner is IActionUsage or IActionDefinition, OwnerRole, expectedOwner);

            if (!string.IsNullOrWhiteSpace(payload.Name))
            {
                this.CheckNameIsFree(owner, payload.Name);
                transition.DeclaredName = payload.Name;
            }

            if (payload.Ends is not { Count: ConnectorEndCount })
            {
                throw new InvalidChangeException("A transition goes from a source to a target: give them as two ends, paths of names from its owner, for example ['launch', 'cruise'].");
            }

            var ends = payload.Ends.Select(path => this.ResolveEnd(owner, path)).ToList();

            for (var endIndex = 0; endIndex < ConnectorEndCount; endIndex++)
            {
                var connectedFeature = ends[endIndex][^1];
                CheckKind(connectedFeature, connectedFeature is IActionUsage, $"end '{payload.Ends[endIndex]}' of a TransitionUsage", "a state or an action");
            }

            var trigger = payload.Trigger == null ? null : this.Resolve(payload.Trigger, TriggerRole);

            if (trigger != null)
            {
                CheckKind(trigger, trigger is IClassifier and not IConjugatedPortDefinition, TriggerRole, "the definition of the event, for example an item definition");
            }

            var guardFeatures = payload.Guard == null ? default : this.ResolveGuard(owner, payload.Guard);

            var effect = payload.Effect == null ? null : this.Resolve(payload.Effect, EffectRole);

            if (effect != null)
            {
                CheckKind(effect, effect is IActionUsage, EffectRole, "an action usage");
            }

            this.AddOwnedMember(owner, transition, change.Identity);
            this.AddTransitionSource(transition, ends[0]);
            this.AddEmptyParameter(transition);

            if (trigger != null)
            {
                this.AddEmptyParameter(transition);
                this.AddTrigger(transition, trigger);
            }

            if (payload.Guard != null)
            {
                var guard = payload.Guard;
                var limitOperand = guardFeatures.Limit == null ? this.CreateValueExpression(guard.Limit.Value, guard.Unit) : this.CreatePathExpression(guardFeatures.Limit);
                var condition = this.CreateOperation(guard.Operator, this.CreatePathExpression(guardFeatures.Attribute), limitOperand);
                this.AddOwnedRelationship(transition, new TransitionFeatureMembership { Kind = TransitionFeatureKind.Guard, Visibility = VisibilityKind.Public }, condition);
            }

            if (effect != null)
            {
                var perform = this.Add(new PerformActionUsage());
                this.AddOwnedRelationship(transition, new TransitionFeatureMembership { Kind = TransitionFeatureKind.Effect, Visibility = VisibilityKind.Public }, perform);
                this.AddOwnedRelationship(perform, new ReferenceSubsetting { ReferencedFeature = effect.Id });
            }

            var succession = this.Add(new Succession());
            this.AddOwnedRelationship(transition, new OwningMembership { Visibility = VisibilityKind.Public }, succession);
            this.AddEnd(succession, ends[0]);
            this.AddEnd(succession, ends[1]);

            if (!string.IsNullOrWhiteSpace(payload.Text))
            {
                this.AddDocumentation(transition, payload.Text);
            }
        }

        /// <summary>
        /// Creates the entry, do or exit action of a state, as <c>entry action initialise;</c> (a new action) or
        /// <c>exit applyBrake;</c> (an existing action, given by <c>performed</c>) do: a <c>PerformActionUsage</c> owned by the
        /// state through a <c>StateSubactionMembership</c> of that kind. An <c>ActionUsage</c> of the payload is built as the
        /// <c>PerformActionUsage</c> that the textual notation gives.
        /// </summary>
        /// <param name="member">The new element, which has no identifier yet.</param>
        /// <param name="change">The change that creates it, whose identity is an optional temporary name.</param>
        /// <exception cref="InvalidChangeException">Thrown when the change cannot be applied.</exception>
        private void CreateStateAction(IElement member, ModelChange change)
        {
            var payload = change.Payload;
            var kind = payload.StateAction.Value;

            if (member is not IPerformActionUsage && member.GetType() != typeof(ActionUsage))
            {
                throw new InvalidChangeException($"Only an ActionUsage or a PerformActionUsage is the entry, do or exit action of a state, not a {member.GetType().Name}.");
            }

            if (!Enum.IsDefined(kind))
            {
                throw new InvalidChangeException("The stateAction must be Entry, Do or Exit.");
            }

            if (payload is not { Value: null, Unit: null, Constraint: null, SatisfiedRequirement: null, SatisfyingPart: null, Ends: null, Direction: null, Conjugated: null, IsEnd: null, Trigger: null, Guard: null, Effect: null, Item: null }
                || !string.IsNullOrWhiteSpace(payload.ReqId))
            {
                throw new InvalidChangeException(
                    "An entry, do or exit action takes an owner state and a name or a performed action, and optionally a definition, specializes and a text.");
            }

            var owner = this.Resolve(payload.Owner, OwnerRole);
            CheckKind(owner, owner is IStateUsage or IStateDefinition, OwnerRole, "a StateUsage or a StateDefinition");

            var hasActionOfKind = this.GetElements(owner.OwnedRelationship)
                .OfType<IStateSubactionMembership>()
                .Any(membership => membership.Kind == kind && this.GetElements(membership.OwnedRelatedElement).Any(action => !this.IsMarkedForDeletion(action)));

            if (hasActionOfKind)
            {
                throw new InvalidChangeException($"The {Describe(owner)} already has an {kind.ToString().ToLowerInvariant()} action: a state has at most one action of each kind.");
            }

            var performed = payload.Performed == null ? null : this.Resolve(payload.Performed, PerformedRole);

            if (performed != null)
            {
                CheckKind(performed, performed is IActionUsage, PerformedRole, "an action usage");
            }
            else if (string.IsNullOrWhiteSpace(payload.Name))
            {
                throw new InvalidChangeException("An entry, do or exit action needs a name (do action operate;) or a performed action (do operate;).");
            }

            var action = member as PerformActionUsage ?? new PerformActionUsage();

            if (!string.IsNullOrWhiteSpace(payload.Name))
            {
                CheckName(payload.Name);
                this.CheckNameIsFree(owner, payload.Name);
                action.DeclaredName = payload.Name;
            }

            var definition = payload.Definition == null ? null : this.ResolveDefinition(action, payload.Definition);
            var general = payload.Specializes == null ? null : this.ResolveGeneral(action, payload.Specializes);

            this.AddOwnedMember(owner, action, change.Identity, new StateSubactionMembership { Kind = kind, Visibility = VisibilityKind.Public });

            if (performed != null)
            {
                this.AddOwnedRelationship(action, new ReferenceSubsetting { ReferencedFeature = performed.Id });
            }

            if (definition != null)
            {
                this.AddTyping(action, definition);
            }

            if (general != null)
            {
                this.SetSpecialization(action, general);
            }

            if (!string.IsNullOrWhiteSpace(payload.Text))
            {
                this.AddDocumentation(action, payload.Text);
            }
        }

        /// <summary>
        /// Gets the definition of what flows through a flow, and checks the ends of the flow: each one is a feature of a
        /// usage, such as <c>camera.dataOut</c>, since the textual notation reads the last name of an end as the feature
        /// that the item leaves or enters.
        /// </summary>
        /// <param name="payload">The payload of the change that creates the flow.</param>
        /// <param name="ends">The features of the path of each end.</param>
        /// <returns>The definition of the item, or <c>null</c> when the payload gives none.</returns>
        /// <exception cref="InvalidChangeException">Thrown when an end or the item is not valid.</exception>
        private IElement CheckFlow(ElementPayload payload, List<List<IElement>> ends)
        {
            for (var endIndex = 0; endIndex < ConnectorEndCount; endIndex++)
            {
                if (ends[endIndex].Count < 2)
                {
                    throw new InvalidChangeException(
                        $"The end '{payload.Ends[endIndex]}' of a FlowUsage must be a feature of a usage, for example 'camera.dataOut' or 'takeImage.image': give the path through the usage that holds it.");
                }
            }

            if (payload.Item == null)
            {
                return null;
            }

            var item = this.Resolve(payload.Item, ItemRole);
            CheckKind(item, item is IClassifier and not IConjugatedPortDefinition, ItemRole, "a definition, for example an item definition");

            return item;
        }

        /// <summary>
        /// Adds to a flow what flows through it, as <c>flow of Image</c> does: a <c>PayloadFeature</c> typed by the
        /// definition of the item.
        /// </summary>
        /// <param name="flow">The flow.</param>
        /// <param name="item">The definition of the item.</param>
        private void AddFlowItem(IElement flow, IElement item)
        {
            var payloadFeature = this.Add(new PayloadFeature());
            this.AddOwnedRelationship(flow, new FeatureMembership { Visibility = VisibilityKind.Public }, payloadFeature);
            this.AddOwnedRelationship(payloadFeature, new FeatureTyping { TypedFeature = payloadFeature.Id, Type = item.Id });
        }

        /// <summary>
        /// Adds an end to a flow, as <c>camera.dataOut</c> in <c>flow from camera.dataOut to obc.dataIn</c> does: a
        /// <c>FlowEnd</c> whose <c>ReferenceSubsetting</c> designates the usage (<c>camera</c>, or a chain such as
        /// <c>payloadSubsystem.camera</c>), and which owns a reference feature that redefines the feature that the item
        /// leaves or enters (<c>dataOut</c>), with its direction.
        /// </summary>
        /// <param name="flow">The flow.</param>
        /// <param name="features">The features of the path of the end, at least two.</param>
        private void AddFlowEnd(IElement flow, List<IElement> features)
        {
            var flowEnd = this.Add(new FlowEnd { IsEnd = true });
            this.AddOwnedRelationship(flow, new EndFeatureMembership { Visibility = VisibilityKind.Public }, flowEnd);
            this.AddReferenceSubsetting(flowEnd, features[..^1]);

            var flowFeature = this.Add(new ReferenceUsage { Direction = ((IFeature)features[^1]).Direction });
            this.AddOwnedRelationship(flowEnd, new FeatureMembership { Visibility = VisibilityKind.Public }, flowFeature);
            this.AddOwnedRelationship(flowFeature, new Redefinition { RedefiningFeature = flowFeature.Id, RedefinedFeature = features[^1].Id });
        }

        /// <summary>
        /// Gets the features that a guard compares, before anything is built.
        /// </summary>
        /// <param name="owner">The owner of the transition, where the names of the guard are looked for first.</param>
        /// <param name="guard">The guard.</param>
        /// <returns>The features of the path of the compared attribute, and those of the limit attribute or <c>null</c>.</returns>
        /// <exception cref="InvalidChangeException">Thrown when the guard is invalid or a name designates no feature.</exception>
        private (List<IElement> Attribute, List<IElement> Limit) ResolveGuard(IElement owner, GuardPayload guard)
        {
            try
            {
                var constraint = new ConstraintPayload { Attribute = guard.Attribute, Operator = guard.Operator, Limit = guard.Limit, LimitAttribute = guard.LimitAttribute, Unit = guard.Unit };
                CheckConstraint(constraint);
            }
            catch (InvalidChangeException exception)
            {
                throw new InvalidChangeException($"The guard is written like a constraint: {char.ToLowerInvariant(exception.Message[0])}{exception.Message[1..]}");
            }

            return (this.ResolveScopePath(owner, guard.Attribute), guard.LimitAttribute == null ? null : this.ResolveScopePath(owner, guard.LimitAttribute));
        }

        /// <summary>
        /// Gets the features of a path of names used in an expression, for example <c>eps.battery.charge</c>: the first name
        /// is looked for among the features of the element and of its definitions, then among those of its owners, as a
        /// name is resolved in the textual notation; each next name among the features of the previous feature and of its
        /// definitions.
        /// </summary>
        /// <param name="context">The element where the first name is looked for first.</param>
        /// <param name="path">The path, already checked by <see cref="CheckPath"/>.</param>
        /// <returns>The features of the path, in order.</returns>
        /// <exception cref="InvalidChangeException">Thrown when a name designates no feature.</exception>
        private List<IElement> ResolveScopePath(IElement context, string path)
        {
            var names = path.Split(PathSeparator).Select(name => name.Trim()).ToList();
            IElement first = null;

            for (var scope = context; scope != null && first == null; scope = this.GetOwner(scope))
            {
                first = this.FindFeature(scope, names[0]);
            }

            if (first == null)
            {
                throw new InvalidChangeException(
                    $"The name '{names[0]}' of '{path}' is not valid: neither the {Describe(context)} nor its owners have a feature with this name. Use list_children to read their features.");
            }

            var features = new List<IElement> { first };

            foreach (var name in names.Skip(1))
            {
                features.Add(this.FindFeature(features[^1], name) ?? throw new InvalidChangeException(
                    $"The name '{name}' of '{path}' is not valid: the {Describe(features[^1])} and its definitions have no feature with this name. Use list_children to read their features."));
            }

            return features;
        }

        /// <summary>
        /// Creates the expression of a path of features, for example <c>eps.battery.charge</c>: a reference to the first
        /// feature, followed by a feature chain expression for each next feature.
        /// </summary>
        /// <param name="features">The features of the path, in order.</param>
        /// <returns>The expression.</returns>
        private IElement CreatePathExpression(List<IElement> features)
        {
            IElement expression = this.CreateReferenceOperand(features[0]);

            foreach (var feature in features.Skip(1))
            {
                expression = this.CreateFeatureChain(expression, feature);
            }

            return expression;
        }

        /// <summary>
        /// Gets the feature of an element or of its definitions that has the given name, unless it is marked for deletion.
        /// </summary>
        /// <param name="element">The element.</param>
        /// <param name="name">The name of the feature.</param>
        /// <returns>The feature, or <c>null</c> when there is none.</returns>
        private IFeature FindFeature(IElement element, string name)
        {
            return this.GetFeatures(element).FirstOrDefault(feature => feature.DeclaredName == name && !this.IsMarkedForDeletion(feature));
        }

        /// <summary>
        /// Adds the source of a transition, as <c>first launch</c> does: its first membership, which designates the source,
        /// or which owns a chain of features for a path such as <c>launch.ascent</c>.
        /// </summary>
        /// <param name="transition">The transition.</param>
        /// <param name="features">The features of the path of the source.</param>
        private void AddTransitionSource(IElement transition, List<IElement> features)
        {
            if (features.Count == 1)
            {
                this.AddOwnedRelationship(transition, new Membership { MemberElement = features[0].Id, Visibility = VisibilityKind.Public });
                return;
            }

            var chain = this.Add(new Feature());
            this.AddOwnedRelationship(transition, new OwningMembership { Visibility = VisibilityKind.Public }, chain);
            this.AddChainings(chain, features);
        }

        /// <summary>
        /// Adds an empty input parameter to a transition, as the textual notation does: one for every transition, and a
        /// second one when it has a trigger.
        /// </summary>
        /// <param name="transition">The transition.</param>
        private void AddEmptyParameter(IElement transition)
        {
            this.AddOwnedRelationship(transition, new ParameterMembership { Visibility = VisibilityKind.Public }, this.Add(new ReferenceUsage { Direction = FeatureDirectionKind.In }));
        }

        /// <summary>
        /// Adds the trigger of a transition, as <c>accept LaunchCompleted</c> does: an <c>AcceptActionUsage</c> whose payload
        /// parameter is typed by the definition of the event.
        /// </summary>
        /// <param name="transition">The transition.</param>
        /// <param name="trigger">The definition of the event.</param>
        private void AddTrigger(IElement transition, IElement trigger)
        {
            var accept = this.Add(new AcceptActionUsage { IsComposite = true });
            this.AddOwnedRelationship(transition, new TransitionFeatureMembership { Kind = TransitionFeatureKind.Trigger, Visibility = VisibilityKind.Public }, accept);

            var payloadParameter = this.Add(new ReferenceUsage { Direction = FeatureDirectionKind.In });
            this.AddOwnedRelationship(accept, new ParameterMembership { Visibility = VisibilityKind.Public }, payloadParameter);
            this.AddOwnedRelationship(payloadParameter, new FeatureTyping { TypedFeature = payloadParameter.Id, Type = trigger.Id });
        }
    }
}
