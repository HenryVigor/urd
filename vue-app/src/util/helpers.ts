export async function get(path: string) {
    const response = await fetch(`http://localhost:5001/api/${path}`, {
        headers: { Accept: 'application/json' },
    })

    if (!response.ok) {
        throw new Error(`Request failed: ${response.status}`)
    }

    return response.json()
}

export async function patch(path: string, body: object) {
    const response = await fetch(`http://localhost:5001/api/${path}`, {
        method: 'PATCH',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(body),
    })

    if (!response.ok) {
        throw new Error(`Request failed: ${response.status}`)
    }

    // No content to parse
    if (response.status === 204) {
        return null
    }

    const text = await response.text()

    return text ? JSON.parse(text) : null
}

export async function post(path: string, body: object) {
    const response = await fetch(`http://localhost:5001/api/${path}`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(body),
    })

    if (!response.ok) {
        throw new Error(`Request failed: ${response.status}`)
    }

    return response.json()
}

export async function del(path: string) {
    const response = await fetch(`http://localhost:5001/api/${path}`, {
        method: 'DELETE',
        headers: { Accept: 'application/json' },
    })

    if (!response.ok) {
        throw new Error(`Request failed: ${response.status}`)
    }

    // No content to parse
    if (response.status === 204) {
        return null
    }

    const text = await response.text()

    return text ? JSON.parse(text) : null
}
