// <copyright>
// Copyright Pillars Inc.
// </copyright>
//
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Linq;

using Rock;
using Rock.Attribute;
using Rock.Data;
using Rock.Model;
using Rock.Web.Cache;
using Rock.Workflow;

namespace rocks.pillars.WorkflowActions.Workflow.Action.Groups
{
    /// <summary>
    /// Sets an attribute's value to the selected person 
    /// </summary>
    [ActionCategory( "Pillars: Groups" )]
    [Description( "Adds a new group." )]
    [Export( typeof( ActionComponent ) )]
    [ExportMetadata( "ComponentName", "Group Add" )]

    [GroupTypeField("Group Type", "The type of group to add.", true, "", "", 3)]
    [WorkflowTextOrAttribute( "Group Name", "Attribute Value", "The name of the group to add. <span class='tip tip-lava'></span>", true, "", "", 4, "GroupName", new string[] { "Rock.Field.Types.TextFieldType" } )]
    [WorkflowAttribute( "Parent Group", "An optional group attribute to use as the parent group. If not selected, the group will be added wihout a parent gruop.", false, "", "", 5, "", new string[] { "Rock.Field.Types.GroupFieldType" } )]
    [WorkflowAttribute( "Result Attribute", "An optional group attribute to set after the group is created.", false, "", "", 6, "", new string[] { "Rock.Field.Types.GroupFieldType" } )]
    [BooleanField( "Check Existing", "If an existing group exists with the same group type, name, and parent group, should that group be used?", true, "", 7)]
    public class GroupAdd : ActionComponent
    {
        /// <summary>
        /// Executes the specified workflow.
        /// </summary>
        /// <param name="rockContext">The rock context.</param>
        /// <param name="action">The action.</param>
        /// <param name="entity">The entity.</param>
        /// <param name="errorMessages">The error messages.</param>
        /// <returns></returns>
        public override bool Execute( RockContext rockContext, WorkflowAction action, Object entity, out List<string> errorMessages )
        {
            errorMessages = new List<string>();

            // Get Group Type
            var groupTypeGuid = GetAttributeValue( action, "GroupType", true ).AsGuid();
            var groupType = GroupTypeCache.Read( groupTypeGuid );
            if ( groupType == null )
            {
                // Appears Group type cache by gui is not always working.  Try to read from db.
                var groupTypeModel = new GroupTypeService( rockContext ).Get( groupTypeGuid );
                if ( groupTypeModel != null )
                {
                    groupType = GroupTypeCache.Read( groupTypeModel.Id ); 
                }
            }
            if ( groupType == null )
            {
                errorMessages.Add( "The Group Type could not be determined or found!" );
            }

            // Group Name
            var groupName = GetAttributeValue( action, "GroupName", true );
            if ( groupName.IsNullOrWhiteSpace() )
            {
                errorMessages.Add( "The Group Type could not be determined or found!" );
            }

            // Parent Group
            Group parentGroup = null;
            var parentGroupGuid = GetAttributeValue( action, "ParentGroup", true ).AsGuidOrNull();
            if ( parentGroupGuid.HasValue )
            {
                parentGroup = new GroupService( rockContext ).Get( parentGroupGuid.Value );
            }

            // Add request
            if ( !errorMessages.Any() )
            {
                var groupService = new GroupService( rockContext );

                Group group = null;

                int? parentGroupId = parentGroup != null ? parentGroup.Id : (int?)null;
                if ( GetAttributeValue( action, "CheckExisting", true ).AsBoolean() )
                {
                    group = groupService.Queryable()
                        .Where( g =>
                            g.GroupTypeId == groupType.Id &&
                            g.Name == groupName &&
                            ( ( !parentGroupId.HasValue && !g.ParentGroupId.HasValue ) || ( parentGroupId.HasValue && g.ParentGroupId.HasValue && g.ParentGroupId.Value == parentGroupId.Value ) ) )
                        .FirstOrDefault();
                }

                if ( group == null )
                {
                    group = new Group();
                    groupService.Add( group );
                    group.GroupTypeId = groupType.Id;
                    group.Name = groupName;

                    if ( parentGroup != null )
                    {
                        group.ParentGroupId = parentGroup.Id;
                    }

                    rockContext.SaveChanges();
                }

                if ( group.Id > 0 )
                {
                    string resultValue = group.Guid.ToString();
                    var attribute = SetWorkflowAttributeValue( action, "ResultAttribute", resultValue );
                    if ( attribute != null )
                    {
                        action.AddLogEntry( string.Format( "Set '{0}' attribute to '{1}'.", attribute.Name, resultValue ) );
                    }
                }
            }

            errorMessages.ForEach( m => action.AddLogEntry( m, true ) );

            return !errorMessages.Any();
        }

    }
}