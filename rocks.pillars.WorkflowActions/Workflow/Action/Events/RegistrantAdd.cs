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
    [Description( "Adds a registrant to an existing registration." )]
    [Export( typeof( ActionComponent ) )]
    [ExportMetadata( "ComponentName", "Registrant Add" )]

    [WorkflowTextOrAttribute( "Registration ID", "Attribute Value", "Text or workflow attribute that contains the Id of the registration that registrant(s) should be added to. <span class='tip tip-lava'></span>", true, "", "", 2, "RegistrationId",
        new string[] { "Rock.Field.Types.IntegerFieldType" } )]
    [WorkflowTextOrAttribute( "Registrant(s)", "Attribute Value", "Text or workflow attribute that contains the person that should be added as a registrant. If using a text value, the values must be a comma-delimited list of person ids to add as registrants. <span class='tip tip-lava'></span>", true, "", "", 3, "Registrants",
        new string[] { "Rock.Field.Types.TextFieldType", "Rock.Field.Types.PersonFieldType" } )]

    public class RegistrantAdd : ActionComponent
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
            Registration registration = new RegistrationService( rockContext ).Get( GetAttributeValue( action, "RegistrationId", true ).AsInteger() );
            if ( registration == null )
            {
                errorMessages.Add( "The Registration could not be determined or found!" );
            }

            // determine the person/p that will be added to the registration instance
            List<Person> people = new List<Person>();
            string registrantsAttributeValue = GetAttributeValue( action, "Registrants", true );
            var personAliasGuid = registrantsAttributeValue.AsGuidOrNull();
            if ( personAliasGuid.HasValue )
            {
                var person = new PersonAliasService( rockContext ).Queryable()
                    .Where( a => a.Guid.Equals( personAliasGuid.Value ) )
                    .Select( a => a.Person )
                    .FirstOrDefault();
                if ( person != null )
                {
                    people.Add( person );
                }
            }
            if ( !people.Any() )
            {
                var personIdList = registrantsAttributeValue.SplitDelimitedValues().AsIntegerList();
                if ( personIdList.Any() )
                {
                    people = new PersonService( rockContext ).Queryable()
                        .Where( p => personIdList.Contains( p.Id ) )
                        .ToList();
                }
            }
            if ( !people.Any() )
            {
                errorMessages.Add( "The Registrant(s) could not be determined or found!" );
            }

            // Add registrant(s)
            if ( !errorMessages.Any() )
            {
                var registrantService = new RegistrationRegistrantService( rockContext );
                foreach ( var person in people )
                {
                    if ( person.PrimaryAliasId.HasValue )
                    {
                        var registrant = new RegistrationRegistrant();
                        registrant.RegistrationId = registration.Id;
                        registrant.PersonAliasId = person.PrimaryAliasId.Value;
                        registrantService.Add( registrant );
                    }
                }

                rockContext.SaveChanges();
            }

            errorMessages.ForEach( m => action.AddLogEntry( m, true ) );

            return true;
        }

    }
}