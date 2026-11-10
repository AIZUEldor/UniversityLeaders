// Profile photos are stored in PostgreSQL, so they are downsized in the browser before upload (the API accepts <= 1 MB).

const MAX_SIDE = 512;

/** Resizes a chosen JPEG/PNG to at most 512x512 px and re-encodes it as JPEG (typically 30-80 KB). */
export async function preparePhoto(file: File): Promise<Blob> {
  if (!["image/jpeg", "image/png"].includes(file.type)) {
    throw new PhotoError("Faqat JPG yoki PNG rasm tanlang.");
  }

  let bitmap: ImageBitmap;
  try {
    bitmap = await createImageBitmap(file);
  } catch {
    throw new PhotoError("Rasmni o'qib bo'lmadi. Boshqa fayl tanlang.");
  }

  const scale = Math.min(1, MAX_SIDE / Math.max(bitmap.width, bitmap.height));
  const canvas = document.createElement("canvas");
  canvas.width = Math.max(1, Math.round(bitmap.width * scale));
  canvas.height = Math.max(1, Math.round(bitmap.height * scale));
  const context = canvas.getContext("2d");
  if (!context) throw new PhotoError("Brauzer rasmni tayyorlay olmadi.");
  context.fillStyle = "#ffffff"; // transparent PNG areas become white in JPEG
  context.fillRect(0, 0, canvas.width, canvas.height);
  context.drawImage(bitmap, 0, 0, canvas.width, canvas.height);
  bitmap.close();

  const blob = await new Promise<Blob | null>((resolve) => canvas.toBlob(resolve, "image/jpeg", 0.85));
  if (!blob) throw new PhotoError("Brauzer rasmni tayyorlay olmadi.");
  return blob;
}

/** A user-facing problem with the chosen file (shown as-is). */
export class PhotoError extends Error {}
