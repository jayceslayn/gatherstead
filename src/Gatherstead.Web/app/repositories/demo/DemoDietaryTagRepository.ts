import type { IDietaryTagRepository } from '../interfaces'
import type { DietaryTag } from '../types'

// Mirrors the backend DietaryTag seed data. Slugs must match exactly what the backend stores.
export const DEMO_DIETARY_TAGS: DietaryTag[] = [
  // Diet
  { id: 'd1e70100-0000-0000-0000-000000000001', slug: 'vegan',            displayName: 'Vegan',             category: 'Diet',        sortOrder: 0 },
  { id: 'd1e70100-0000-0000-0000-000000000002', slug: 'vegetarian',       displayName: 'Vegetarian',        category: 'Diet',        sortOrder: 1 },
  { id: 'd1e70100-0000-0000-0000-000000000003', slug: 'pescatarian',      displayName: 'Pescatarian',       category: 'Diet',        sortOrder: 2 },
  { id: 'd1e70100-0000-0000-0000-000000000004', slug: 'flexitarian',      displayName: 'Flexitarian',       category: 'Diet',        sortOrder: 3 },
  { id: 'd1e70100-0000-0000-0000-000000000005', slug: 'halal',            displayName: 'Halal',             category: 'Diet',        sortOrder: 4 },
  { id: 'd1e70100-0000-0000-0000-000000000006', slug: 'kosher',           displayName: 'Kosher',            category: 'Diet',        sortOrder: 5 },
  { id: 'd1e70100-0000-0000-0000-000000000007', slug: 'paleo',            displayName: 'Paleo',             category: 'Diet',        sortOrder: 6 },
  { id: 'd1e70100-0000-0000-0000-000000000008', slug: 'keto',             displayName: 'Keto',              category: 'Diet',        sortOrder: 7 },
  { id: 'd1e70100-0000-0000-0000-000000000009', slug: 'raw-food',         displayName: 'Raw Food',          category: 'Diet',        sortOrder: 8 },
  // Allergy
  { id: 'd1e70200-0000-0000-0000-000000000001', slug: 'peanut-allergy',   displayName: 'Peanut Allergy',    category: 'Allergy',     sortOrder: 0 },
  { id: 'd1e70200-0000-0000-0000-000000000002', slug: 'tree-nut-allergy', displayName: 'Tree Nut Allergy',  category: 'Allergy',     sortOrder: 1 },
  { id: 'd1e70200-0000-0000-0000-000000000003', slug: 'shellfish-allergy',displayName: 'Shellfish Allergy', category: 'Allergy',     sortOrder: 2 },
  { id: 'd1e70200-0000-0000-0000-000000000004', slug: 'fish-allergy',     displayName: 'Fish Allergy',      category: 'Allergy',     sortOrder: 3 },
  { id: 'd1e70200-0000-0000-0000-000000000005', slug: 'milk-allergy',     displayName: 'Milk Allergy',      category: 'Allergy',     sortOrder: 4 },
  { id: 'd1e70200-0000-0000-0000-000000000006', slug: 'egg-allergy',      displayName: 'Egg Allergy',       category: 'Allergy',     sortOrder: 5 },
  { id: 'd1e70200-0000-0000-0000-000000000007', slug: 'soy-allergy',      displayName: 'Soy Allergy',       category: 'Allergy',     sortOrder: 6 },
  { id: 'd1e70200-0000-0000-0000-000000000008', slug: 'wheat-allergy',    displayName: 'Wheat Allergy',     category: 'Allergy',     sortOrder: 7 },
  { id: 'd1e70200-0000-0000-0000-000000000009', slug: 'sesame-allergy',   displayName: 'Sesame Allergy',    category: 'Allergy',     sortOrder: 8 },
  // Restriction
  { id: 'd1e70300-0000-0000-0000-000000000001', slug: 'gluten-free',        displayName: 'Gluten Free',       category: 'Restriction', sortOrder: 0  },
  { id: 'd1e70300-0000-0000-0000-000000000002', slug: 'dairy-free',         displayName: 'Dairy Free',        category: 'Restriction', sortOrder: 1  },
  { id: 'd1e70300-0000-0000-0000-000000000003', slug: 'lactose-intolerant', displayName: 'Lactose Intolerant',category: 'Restriction', sortOrder: 2  },
  { id: 'd1e70300-0000-0000-0000-000000000004', slug: 'nut-free',           displayName: 'Nut Free',          category: 'Restriction', sortOrder: 3  },
  { id: 'd1e70300-0000-0000-0000-000000000005', slug: 'low-sodium',         displayName: 'Low Sodium',        category: 'Restriction', sortOrder: 4  },
  { id: 'd1e70300-0000-0000-0000-000000000006', slug: 'low-sugar',          displayName: 'Low Sugar',         category: 'Restriction', sortOrder: 5  },
  { id: 'd1e70300-0000-0000-0000-000000000007', slug: 'diabetic-friendly',  displayName: 'Diabetic Friendly', category: 'Restriction', sortOrder: 6  },
  { id: 'd1e70300-0000-0000-0000-000000000008', slug: 'low-fodmap',         displayName: 'Low FODMAP',        category: 'Restriction', sortOrder: 7  },
  { id: 'd1e70300-0000-0000-0000-000000000009', slug: 'no-pork',            displayName: 'No Pork',           category: 'Restriction', sortOrder: 8  },
  { id: 'd1e70300-0000-0000-0000-00000000000a', slug: 'no-alcohol',         displayName: 'No Alcohol',        category: 'Restriction', sortOrder: 9  },
  { id: 'd1e70300-0000-0000-0000-00000000000b', slug: 'no-shellfish',       displayName: 'No Shellfish',      category: 'Restriction', sortOrder: 10 },
]

export class DemoDietaryTagRepository implements IDietaryTagRepository {
  async listDietaryTags(): Promise<DietaryTag[]> {
    return DEMO_DIETARY_TAGS
  }
}
