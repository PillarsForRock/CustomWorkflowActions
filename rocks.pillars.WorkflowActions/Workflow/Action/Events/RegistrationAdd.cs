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
using Rock.Workflow;

namespace rocks.pillars.WorkflowActions.Workflow.Action.Events
{
    /// <summary>
    /// Sets an attribute's value to the selected person 
    /// </summary>
    [ActionCategory( "Pillars: Events" )]
    [Description( "Adds a registration and registrar to a specific event instance, and returns the registration id." )]
    [Export( typeof( ActionComponent ) )]
    [ExportMetadata( "ComponentName", "Registration Add" )]

    [WorkflowTextOrAttribute( "Registration Instance ID", "Attribute Value", "Registration instance that the registration should be added to. <span class='tip tip-lava'></span>", true, "", "", 2, "RegistrationInstanceId",
        new string[] { "Rock.Field.Types.IntegerFieldType" } )]
    [WorkflowAttribute( "Registrar", "Workflow attribute that contains the person to add as the registrar.", true, "", "", 3, null,
        new string[] { "Rock.Field.Types.PersonFieldType" } )]
    [WorkflowAttribute( "Result Attribute", "An optional attribute to set to the registration id that is created.", false, "", "", 4 )]

    public class RegistrationAdd : ActionComponent
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

            // get the registration instance
            RegistrationInstance instance = new RegistrationInstanceService( rockContext ).Get( GetAttributeValue( action, "RegistrationInstanceId", true ).AsInteger() );
            if ( instance == null )
            {
                errorMessages.Add( "The Registration Instance could not be determined or found!" );
            }

            // determine the person that will be added to the registration instance
            Person person = null;
            var personAliasGuid = GetAttributeValue( action, "Registrar", true ).AsGuidOrNull();
            if ( personAliasGuid.HasValue )
            {
                person = new PersonAliasService( rockContext ).Queryable()
                    .Where( a => a.Guid.Equals( personAliasGuid.Value ) )
                    .Select( a => a.Person )
                    .FirstOrDefault();
            }
            if ( person == null || !person.PrimaryAliasId.HasValue )
            {
                errorMessages.Add( "The Person for the Registrar value could not be determined or found!" );
            }

            // Add registration
            if ( !errorMessages.Any() )
            {
                var registrationService = new RegistrationService( rockContext );

                var registration = new Registration();
                registrationService.Add( registration );
                registration.RegistrationInstanceId = instance.Id;
                registration.PersonAliasId = person.PrimaryAliasId.Value;
                registration.FirstName = person.NickName;
                registration.LastName = person.LastName;
                registration.IsTemporary = false;
                registration.ConfirmationEmail = person.Email;

                rockContext.SaveChanges();

                if ( registration.Id > 0 )
                {
                    string resultValue = registration.Id.ToString();
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