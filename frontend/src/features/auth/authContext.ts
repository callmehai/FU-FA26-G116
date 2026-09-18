import { createContext } from 'react'
import type { Capability, Role, User } from '../../types/user'

export interface AuthContextValue {
  user: User | null
  /** UI-only check. The backend re-authorizes every request regardless of what this returns. */
  can: (capability: Capability) => boolean
  signIn: (email: string, role: Role) => void
  signOut: () => void
}

/** Kept apart from the provider so the module exports only a context — fast refresh needs that. */
export const AuthContext = createContext<AuthContextValue | null>(null)
