/** A published phone number as a tap-to-call link. */
export function PhoneLink({ phone, className = "text-sm text-indigo-700 hover:underline" }: { phone: string; className?: string }) {
  return <a href={`tel:${phone.replace(/[^+\d]/g, "")}`} className={className}>{phone}</a>;
}
