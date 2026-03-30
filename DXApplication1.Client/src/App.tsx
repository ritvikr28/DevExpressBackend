import { Outlet } from 'react-router-dom';
import NavMenu from './components/NavMenu';

export function App() {
    return (
        <>
            <NavMenu />
            <Outlet />
        </>
    );
}