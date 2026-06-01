// Auto-generated from OpenAPI spec — do not edit.
// Run scripts/generate-openapi.sh to regenerate.

export type paths = Record<string, never>;
export type webhooks = Record<string, never>;

export interface components {
  schemas: {
    // Enums (serialized as strings via JsonStringEnumConverter)
    TenantRole: "Owner" | "Manager" | "Coordinator" | "Member" | "Guest";
    HouseholdRole: "Manager" | "Member";
    AttendanceStatus: "Going" | "Maybe" | "NotGoing";
    MealType: "Breakfast" | "Lunch" | "Dinner";
    TaskTimeSlot: "Morning" | "Midday" | "Evening" | "Anytime";
    AccommodationType: "Bedroom" | "Bunk" | "RvPad" | "Tent" | "Offsite";
    AccommodationIntentStatus: "Intent" | "Hold" | "Confirmed";
    AccommodationIntentDecision: "Pending" | "Approved" | "Declined";
    InvitationStatus: "Pending" | "Accepted" | "Revoked";
    DietaryCategory: "Diet" | "Allergy" | "Restriction";
    ContactMethodType: "Email" | "Phone" | "Other";
    RelationshipType: "Parent" | "Child" | "Sibling" | "Spouse" | "Guardian" | "Other";

    // Attributes
    AttributeDto: {
      id: string;
      key: string;
      value: string;
      tenantMinRole: number;
      householdMinRole?: number | null;
    };
    AttributeWriteEntry: {
      key: string;
      value: string;
      tenantMinRole: number;
      householdMinRole?: number | null;
    };

    // Tenants
    TenantDto: {
      id: string;
      name: string;
      notes: string | null;
      createdAt: string;
      updatedAt: string;
      isDeleted: boolean;
      deletedAt: string | null;
      deletedByUserId: string | null;
      attributes: components["schemas"]["AttributeDto"][];
    };
    TenantSummary: {
      id: string;
      name: string;
      userRole: components["schemas"]["TenantRole"] | null;
    };

    // TenantUsers
    TenantUserDto: {
      userId: string;
      tenantId: string;
      role: components["schemas"]["TenantRole"];
      linkedMemberId: string | null;
      externalId: string;
    };
    TenantUserMeDto: {
      userId: string;
      tenantId: string;
      role: components["schemas"]["TenantRole"];
      linkedMemberId: string | null;
      linkedHouseholdId: string | null;
      externalId: string;
    };

    // Households
    HouseholdDto: {
      id: string;
      tenantId: string;
      name: string;
      notes: string | null;
      createdAt: string;
      updatedAt: string;
      isDeleted: boolean;
      deletedAt: string | null;
      deletedByUserId: string | null;
      attributes: components["schemas"]["AttributeDto"][];
    };
    HouseholdUserDto: {
      userId: string;
      tenantId: string;
      householdId: string;
      role: components["schemas"]["HouseholdRole"];
      externalId: string;
    };

    // HouseholdMembers
    HouseholdMemberDto: {
      id: string;
      tenantId: string;
      householdId: string;
      name: string;
      isAdult: boolean;
      ageBand: string | null;
      birthDate: string | null;
      dietaryNotes: string | null;
      dietaryTags: string[];
      notes: string | null;
      createdAt: string;
      updatedAt: string;
      isDeleted: boolean;
      deletedAt: string | null;
      deletedByUserId: string | null;
      attributes: components["schemas"]["AttributeDto"][];
    };

    // Addresses
    AddressDto: {
      id: string;
      tenantId: string;
      householdMemberId: string;
      line1: string;
      line2: string | null;
      city: string;
      state: string;
      postalCode: string;
      country: string;
      isPrimary: boolean;
      createdAt: string;
      updatedAt: string;
      isDeleted: boolean;
      deletedAt: string | null;
      deletedByUserId: string | null;
    };

    // ContactMethods
    ContactMethodDto: {
      id: string;
      tenantId: string;
      householdMemberId: string;
      type: components["schemas"]["ContactMethodType"];
      value: string;
      isPrimary: boolean;
      createdAt: string;
      updatedAt: string;
      isDeleted: boolean;
      deletedAt: string | null;
      deletedByUserId: string | null;
    };

    // MemberRelationships
    MemberRelationshipDto: {
      id: string;
      tenantId: string;
      householdMemberId: string;
      relatedMemberId: string;
      relationshipType: components["schemas"]["RelationshipType"];
      notes: string | null;
      createdAt: string;
      updatedAt: string;
      isDeleted: boolean;
      deletedAt: string | null;
      deletedByUserId: string | null;
    };

    // DietaryTags
    DietaryTagDto: {
      id: string;
      slug: string;
      displayName: string;
      category: components["schemas"]["DietaryCategory"];
      sortOrder: number;
    };

    // Invitations
    InvitationDto: {
      id: string;
      tenantId: string;
      email: string;
      role: components["schemas"]["TenantRole"];
      householdId: string | null;
      householdRole: components["schemas"]["HouseholdRole"] | null;
      status: components["schemas"]["InvitationStatus"];
      createdAt: string;
      acceptedAt: string | null;
    };

    // Bootstrap
    BootstrapTenantDto: {
      tenantId: string;
      role: components["schemas"]["TenantRole"];
    };
    UserBootstrapDto: {
      userId: string;
      claimedInvitations: number;
      tenants: components["schemas"]["BootstrapTenantDto"][];
    };

    // Properties
    PropertyDto: {
      id: string;
      tenantId: string;
      name: string;
      notes: string | null;
      createdAt: string;
      updatedAt: string;
      isDeleted: boolean;
      deletedAt: string | null;
      deletedByUserId: string | null;
      attributes: components["schemas"]["AttributeDto"][];
    };

    // Accommodations
    AccommodationDto: {
      id: string;
      tenantId: string;
      propertyId: string;
      name: string;
      type: components["schemas"]["AccommodationType"];
      capacityAdults: number | null;
      capacityChildren: number | null;
      notes: string | null;
      createdAt: string;
      updatedAt: string;
      isDeleted: boolean;
      deletedAt: string | null;
      deletedByUserId: string | null;
      attributes: components["schemas"]["AttributeDto"][];
    };
    AccommodationIntentDto: {
      id: string;
      tenantId: string;
      accommodationId: string;
      householdMemberId: string;
      night: string;
      status: components["schemas"]["AccommodationIntentStatus"];
      notes: string | null;
      decision: components["schemas"]["AccommodationIntentDecision"];
      partySize: number | null;
      priority: number | null;
      createdAt: string;
      updatedAt: string;
      isDeleted: boolean;
      deletedAt: string | null;
      deletedByUserId: string | null;
    };

    // Equipment
    EquipmentDto: {
      id: string;
      tenantId: string;
      propertyId: string | null;
      name: string;
      notes: string | null;
      createdAt: string;
      updatedAt: string;
      isDeleted: boolean;
      deletedAt: string | null;
      deletedByUserId: string | null;
      attributes: components["schemas"]["AttributeDto"][];
    };

    // Events
    EventDto: {
      id: string;
      tenantId: string;
      propertyId: string;
      name: string;
      startDate: string;
      endDate: string;
      notes: string | null;
      createdAt: string;
      updatedAt: string;
      isDeleted: boolean;
      deletedAt: string | null;
      deletedByUserId: string | null;
      attributes: components["schemas"]["AttributeDto"][];
    };

    // EventAttendance
    EventAttendanceDto: {
      id: string;
      tenantId: string;
      eventId: string;
      householdMemberId: string;
      day: string;
      status: components["schemas"]["AttendanceStatus"];
      arrivalWindowStart: string | null;
      arrivalWindowEnd: string | null;
      departureWindowStart: string | null;
      departureWindowEnd: string | null;
      notes: string | null;
      createdAt: string;
      updatedAt: string;
      isDeleted: boolean;
      deletedAt: string | null;
      deletedByUserId: string | null;
    };

    // MealTemplates (mealTypes is integer — flags enum with JsonNumberEnumConverter)
    MealTemplateDto: {
      id: string;
      tenantId: string;
      eventId: string;
      name: string;
      mealTypes: number;
      startDate: string | null;
      endDate: string | null;
      notes: string | null;
      createdAt: string;
      updatedAt: string;
      isDeleted: boolean;
      deletedAt: string | null;
      deletedByUserId: string | null;
      attributes: components["schemas"]["AttributeDto"][];
    };

    // MealPlans
    MealPlanDto: {
      id: string;
      tenantId: string;
      mealTemplateId: string;
      day: string;
      mealType: components["schemas"]["MealType"];
      notes: string | null;
      isException: boolean;
      exceptionReason: string | null;
      createdAt: string;
      updatedAt: string;
      isDeleted: boolean;
      deletedAt: string | null;
      deletedByUserId: string | null;
    };

    // MealAttendance
    MealAttendanceDto: {
      id: string;
      tenantId: string;
      mealPlanId: string;
      householdMemberId: string;
      status: components["schemas"]["AttendanceStatus"];
      bringOwnFood: boolean;
      notes: string | null;
      createdAt: string;
      updatedAt: string;
      isDeleted: boolean;
      deletedAt: string | null;
      deletedByUserId: string | null;
    };

    // MealIntents
    MealIntentDto: {
      id: string;
      tenantId: string;
      mealPlanId: string;
      householdMemberId: string;
      volunteered: boolean;
      createdAt: string;
      updatedAt: string;
      isDeleted: boolean;
      deletedAt: string | null;
      deletedByUserId: string | null;
    };

    // TaskTemplates (timeSlots is integer — flags enum with JsonNumberEnumConverter)
    TaskTemplateDto: {
      id: string;
      tenantId: string;
      eventId: string;
      name: string;
      timeSlots: number;
      startDate: string | null;
      endDate: string | null;
      minimumAssignees: number | null;
      notes: string | null;
      createdAt: string;
      updatedAt: string;
      isDeleted: boolean;
      deletedAt: string | null;
      deletedByUserId: string | null;
      attributes: components["schemas"]["AttributeDto"][];
    };

    // TaskPlans
    TaskPlanDto: {
      id: string;
      tenantId: string;
      templateId: string;
      day: string;
      timeSlot: components["schemas"]["TaskTimeSlot"] | null;
      completed: boolean;
      notes: string | null;
      isException: boolean;
      exceptionReason: string | null;
      createdAt: string;
      updatedAt: string;
      isDeleted: boolean;
      deletedAt: string | null;
      deletedByUserId: string | null;
    };

    // TaskIntents
    TaskIntentDto: {
      id: string;
      tenantId: string;
      taskPlanId: string;
      householdMemberId: string;
      volunteered: boolean;
      createdAt: string;
      updatedAt: string;
      isDeleted: boolean;
      deletedAt: string | null;
      deletedByUserId: string | null;
    };

    // Reports
    EventReportDto: {
      eventId: string;
      eventName: string;
      startDate: string;
      endDate: string;
      days: components["schemas"]["EventReportDayDto"][];
    };
    EventReportDayDto: {
      day: string;
      going: number;
      maybe: number;
      meals: components["schemas"]["EventReportMealDto"][];
    };
    EventReportMealDto: {
      mealPlanId: string;
      templateName: string;
      mealType: components["schemas"]["MealType"];
      going: number;
      maybe: number;
      notGoing: number;
      bringOwnFood: number;
      dietary: components["schemas"]["DietaryTallyDto"][];
      attendees: components["schemas"]["EventReportAttendeeDto"][];
    };
    DietaryTallyDto: {
      label: string;
      count: number;
    };
    EventReportAttendeeDto: {
      memberId: string;
      name: string;
      status: components["schemas"]["AttendanceStatus"];
      bringOwnFood: boolean;
      dietary: string[];
      dietaryNotes: string | null;
    };
  };
  responses: never;
  parameters: never;
  requestBodies: never;
  headers: never;
  pathItems: never;
}

export type $defs = Record<string, never>;
export type external = Record<string, never>;
export type operations = Record<string, never>;
