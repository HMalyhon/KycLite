import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import { withPrimeVue } from '../test/helpers'
import UploadCard from './UploadCard.vue'

const PNG_BYTES = new Uint8Array([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a])

function file(name: string, type: string, size = PNG_BYTES.length) {
  const f = new File([PNG_BYTES], name, { type })
  // Fake the size rather than allocating megabytes just to trip a limit.
  Object.defineProperty(f, 'size', { value: size })
  return f
}

const card = () => mount(UploadCard, { ...withPrimeVue, props: { modelValue: null } })

/** Drops a file on the zone — the path the `accept` attribute cannot police. */
async function drop(wrapper: ReturnType<typeof card>, f: File) {
  await wrapper.find('.dropzone').trigger('drop', { dataTransfer: { files: [f] } })
}

const selected = (wrapper: ReturnType<typeof card>) =>
  wrapper.emitted('update:modelValue')?.at(-1)?.[0] ?? null

describe('UploadCard — accepting a document', () => {
  it('accepts a supported file and hands it up', async () => {
    const wrapper = card()

    await drop(wrapper, file('id.png', 'image/png'))

    expect((selected(wrapper) as File).name).toBe('id.png')
    expect(wrapper.find('.file-error').exists()).toBe(false)
  })

  it('accepts a PDF, which has no image preview', async () => {
    const wrapper = card()

    await drop(wrapper, file('id.pdf', 'application/pdf'))

    expect((selected(wrapper) as File).name).toBe('id.pdf')
  })
})

describe('UploadCard — refusing a document before it is uploaded', () => {
  it('refuses a format the server does not support', async () => {
    // HEIC is the iPhone camera default, so this is the likeliest first-contact failure.
    const wrapper = card()

    await drop(wrapper, file('photo.heic', 'image/heic'))

    expect(wrapper.find('.file-error').text()).toContain('not a supported format')
    expect(selected(wrapper)).toBeNull()
  })

  it('refuses a file over the 10 MB cap and names the size', async () => {
    const wrapper = card()

    await drop(wrapper, file('huge.png', 'image/png', 11 * 1024 * 1024))

    expect(wrapper.find('.file-error').text()).toContain('11.0 MB')
    expect(wrapper.find('.file-error').text()).toContain('10 MB limit')
    expect(selected(wrapper)).toBeNull()
  })

  it('clears the previous selection rather than silently keeping it', async () => {
    // What the card shows must always be what would be submitted.
    const wrapper = card()
    await drop(wrapper, file('good.png', 'image/png'))
    expect(selected(wrapper)).not.toBeNull()

    await drop(wrapper, file('photo.webp', 'image/webp'))

    expect(selected(wrapper)).toBeNull()
  })

  it('recovers when a valid file follows a rejected one', async () => {
    const wrapper = card()
    await drop(wrapper, file('photo.webp', 'image/webp'))
    expect(wrapper.find('.file-error').exists()).toBe(true)

    await drop(wrapper, file('id.png', 'image/png'))

    expect(wrapper.find('.file-error').exists()).toBe(false)
    expect((selected(wrapper) as File).name).toBe('id.png')
  })

  it('resets the input so re-picking the same file still fires a change event', async () => {
    // Without this a user who retries the file they just chose gets no feedback at all.
    const wrapper = card()
    const input = wrapper.find('input[type="file"]').element as HTMLInputElement

    await drop(wrapper, file('photo.webp', 'image/webp'))

    expect(input.value).toBe('')
  })
})

describe('UploadCard — the picker', () => {
  it('offers only the formats the server accepts', () => {
    // Mirrors FileSignatures.SupportedMediaTypes; extensions and media types are both listed
    // because desktop and mobile pickers honour them differently.
    const accept = card().find('input[type="file"]').attributes('accept') ?? ''

    for (const token of ['.jpg', '.png', '.tif', '.tiff', '.pdf', 'image/jpeg', 'application/pdf'])
      expect(accept).toContain(token)

    expect(accept).not.toContain('image/*')
  })
})
