import { BrowserRouter, Route, Routes } from 'react-router-dom'
import { ProtectedRoute } from './components/ProtectedRoute'
import { AuthProvider } from './context/AuthContext'
import { AppLayout } from './layouts/AppLayout'
import { HomePage } from './pages/HomePage'
import { LoginPage } from './pages/LoginPage'
import { NotFoundPage } from './pages/NotFoundPage'
import { RegisterPage } from './pages/RegisterPage'
import { CoursesPage } from './pages/CoursesPage'
import { CourseDetailPage } from './pages/CourseDetailPage'
import { DashboardPage } from './pages/DashboardPage'
import { LearningPage } from './pages/LearningPage'
function Placeholder({ title }: { title: string }) { return <section className="empty"><h1>{title}</h1><p>This feature is the next implementation step.</p></section> }
export default function App() { return <AuthProvider><BrowserRouter><Routes><Route element={<AppLayout />}><Route index element={<HomePage />} /><Route path="courses" element={<CoursesPage />} /><Route path="courses/:slug" element={<CourseDetailPage />} /><Route path="login" element={<LoginPage />} /><Route path="register" element={<RegisterPage />} /><Route path="forgot-password" element={<Placeholder title="Reset password" />} /><Route element={<ProtectedRoute />}><Route path="dashboard" element={<DashboardPage />} /><Route path="learn/:slug" element={<LearningPage />} /></Route><Route element={<ProtectedRoute adminOnly />}><Route path="admin/courses/new" element={<Placeholder title="Create course" />} /></Route><Route path="*" element={<NotFoundPage />} /></Route></Routes></BrowserRouter></AuthProvider> }
