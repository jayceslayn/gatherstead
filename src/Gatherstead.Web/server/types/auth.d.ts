declare module '#auth-utils' {
  interface User {
    id: string
    name: string
    email: string
  }

  interface SecureSessionData {
    accessToken: string
  }
}

export {}
